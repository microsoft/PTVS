# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.

"""Cache layer

Pyvot must assume that the contents of an Excel workbook can change at any time, due to interactive manipulation 
or other external influences. Living out of process, this can be a performance catastrophe. We use a cache when
possible, on the assumption that Excel doesn't change during any single call into to the Pyvot user API.

This module provides the :func:`@cache_result` decorator which adds caching to a function or property,
an :func:`@enable_caching` decorator for enabling the (off by default) cache for the duration of a function call, and the
CacheManager, for non-decorator cache control including invalidation."""

import contextlib
import functools

def enable_caching(f):
    """Decorator which enables caching within the wrapped function. Caching is enabled
    until the function exits; i.e. functions called directly or indirectly will also have
    caching enabled."""
    import functools
    @functools.wraps(f)
    def _wrapped(*args, **kwargs):
        with CacheManager.caching_enabled():
            return f(*args, **kwargs)
    return _wrapped

class _ResultCachingDescriptor(object):
    """Decorator class for caching the results of method calls / read-only properties. The cache is controlled
    by the state of the singleton CacheManager. While caching is enabled, the wrapped function / property
    is called only once per unique set of arguments, and the return value is stored to satisfy future
    calls with those same arguments. When caching is disabled, all cached values are cleared, and
    the wrapped function is always called.

    This decorator may only be applied to a method or property within a class. A separate cache is maintained *per instance*; 
    although argument sets are typically compared for value equality, two equal instances still have separate caches.
    
    Cache statistics are available via the 'stats' attribute (unless wrapping a property):
        
        instance_a.cached_method -> CacheSite instance
        instance_a.cached_method.stats.hits -> # of cache hits (similarly for misses)
        instance_b.cached_method -> Different CacheSite instance

    This information is summarized by CacheManager.cache_info(), in which the per-instance stats are aggregated by class"""

    def __init__(self, f, as_property=False):
        assert callable(f) or isinstance(f, property)
        self._wrapped = f
        self._wrapping_property = isinstance(f, property)

        # SomeClass.cached_thing returns a _ResultCachingDescriptor instance (see __get__)
        # That means that SomeClass.cached_thing.__doc__ should be something useful.
        # This call does _not_ update _ResultCachingDescriptor.__doc__
        self._update_wrapper(self)

    def _update_wrapper(self, o):
        """Updates the given object with __name__, __doc__, etc. from the wrapped thing.
        Just like functools.update_wrapper, but handles the case where we are wrapping a property"""
        wrapped_func = self._wrapped.fget if self._wrapping_property else self._wrapped
        functools.update_wrapper(o, wrapped_func)

    def __get__(self, instance, owning_class=None):
        # This is called as part of the "descriptor protocol," and handles the "method in a class" case
        # http://docs.python.org/reference/datamodel.html#implementing-descriptors

        # We'll allow SomeClass.cached_method to return this object, though
        # it currently doesn't contain anything useful. This is convenient for introspection
        # (why is cached_method behaving strangely? oh, it's a descriptor of some kind)
        # and is consistent with the behavior of the @property descriptor
        if instance is None: return self
        
        # We store instance-level cache sites on the instances themselves, indexed by the owning _ResultCachingDescriptor
        # It produces the following reference structure:
        #   CacheManager - - (weakref) - -> CacheSite <==> instance ==> class ==> this object
        # i.e., the CacheSite and its stored data will be garbage collected along with the instance.
        #
        # This is better than storing a map of instances -> sites on this object, because it would would prevent such reclamation
        #   CacheManager - - (weakref) - -> CacheSite <==> instance ==> class ==> this object\
        #                                        ^===========================================
        # i.e., instances and cache sites would be kept alive by the owning class!
        try:
            instance_sites = instance.__cache_sites
        except AttributeError:
            instance_sites = instance.__cache_sites = {}

        if not self in instance_sites:

            if self._wrapping_property:
                # self._wrapped is a property instance, i.e. we are wrapping another descriptor. Here we construct
                # a CacheSite-compatible callable that binds a particular instance for the property
                def _wrapped_with_instance(): return self._wrapped.__get__(instance, owning_class)
                # str(property object) doesn't give the name of what we are wrapping - however, the getter itself is available
                site_name = "%s (instance of %s at %x)" % (repr(self._wrapped.fget), str(owning_class), id(instance))
            else:
                # self._wrapped is a function, not a bound method. Here, we bind 'self'
                # The resulting CacheSite calls it as an ordinary function.
                def _wrapped_with_instance(*args, **kwargs): return self._wrapped(instance, *args, **kwargs)
                site_name = "%s (instance of %s at %x)" % (repr(self._wrapped), str(owning_class), id(instance))
            # We use _wrapped rather than _wrapped_with_instance for the key, since the latter is unique per instance
            # _wrapped.fget is used if _wrapped is a property, since its __repr__ isn't informative
            wrapped_key = self._wrapped if not self._wrapping_property else self._wrapped.fget
            # CacheSite uses reports the __name__, __doc__, etc. of the function we give it, so update them appropriately
            # This is important for instance.cached_thing.__name__ to work right.
            self._update_wrapper(_wrapped_with_instance)
            instance_sites[self] = CacheManager.create_cache_site(_wrapped_with_instance, site_name, 
                                                                   site_group_key=(wrapped_key, type(instance)))

        if self._wrapping_property:
            return instance_sites[self]()
        else:
            return instance_sites[self]
        
    def __call__(self, *args, **kwargs):
        raise TypeError("_ResultCachingDescriptor is not callable. Only methods within a class (not normal functions) may be cached")

cache_result = _ResultCachingDescriptor

class CacheSite(object):
    """Represents a single cache of arguments -> results.
    Note that there can be multiple cache sites per @cache_result-wrapped method;
    each instance with the caching method uses a separate cache site"""

    def __init__(self, source, site_name=None):
        assert callable(source)
        if site_name is None: site_name = repr(self)
        
        self.source = source
        self.stats = CacheSiteStats()
        self.site_name = site_name
        self._cached = {}

        # Copy __doc__, etc. to the instance.
        # __doc__, etc. on the class itself are preserved
        functools.update_wrapper(self, source)

    def clear(self):
        self._cached.clear()

    def _key(self, *args, **kwargs):
        # kwargs (a dict) is not hashable, but its item tuples may be
        # Tuple conversion needed because lists are not hashable (since mutable)
        return (args, tuple(sorted(kwargs.items())))

    def get(self, *args, **kwargs):
        if not CacheManager.is_caching_enabled:
            self.stats.uncached_misses += 1
            return self.source(*args, **kwargs)

        k = self._key(*args, **kwargs)
        if k in self._cached:
            self.stats.hits += 1
            return self._cached[k]
        else:
            self.stats.misses += 1
            v = self.source(*args, **kwargs)
            self._cached[k] = v
            return v

    __call__ = get

class CacheSiteStats(object):
    """Container for :attr:`hits`, :attr:`misses`, and :attr:`uncached_misses`
    (misses that occurred with cachind disabled). Accessed as :attr:`CacheSite.stats`"""
    def __init__(self):
        self.hits = self.misses = self.uncached_misses = 0

class CacheManager_class(object):
    """Singleton manager for the program's CacheSites (created through use of @:func:`cache_result`)
    Cache state is dynamically scoped on the stack by use of a context manager::

        with CacheManager.caching_enabled():
            do_stuff()

    Within that context, all @cache_result decorators are enabled and may store / return cached values
    Cached values are deleted when the context is exited. 
    
    The context may be safely nested."""
    def __init__(self):
        self._cache_level = 0
        self._site_weakrefs = set()
        self._site_stats = {}
        self._iterating_site_weakrefs = False

    @contextlib.contextmanager
    def caching_enabled(self):
        """Returns an object implementing the context-manager protocol. Within the context,
        caching is enabled (this is a context-manager version of the `@enable_caching` decorator).
        
        Cache activation may be nested; there is no harm in enabling caching before calling a function
        which does the same::
        
            with xl.CacheManager.caching_enabled():
                with xl.CacheManager.caching_enabled():
                    assert xl.CacheManager.is_caching_enabled()
                assert xl.CacheManager.is_caching_enabled()
            assert not xl.CacheManager.is_caching_enabled()"""
        self._increment_cache_level()
        try:
            yield
        finally:
            self._decrement_cache_level()

    @contextlib.contextmanager
    def caching_disabled(self):
        """Returns an object implementing the context-manager protocol. Within the context, caching is
        disabled. When exiting the context, the cache-enable state (incl. nesting level) is restored to its
        previous value. Entering the context immediately invalidates all cache sites
        
        ::

            with xl.CacheManager.caching_enabled():
                with xl.CacheManager.caching_disabled():
                    assert not xl.CacheManager.is_caching_enabled()
                assert xl.CacheManager.is_caching_enabled()"""
        old_level = self._cache_level
        if old_level > 0:
            self._cache_level = 0
            self.invalidate_all_caches()
        try:
            yield
        finally:
            self._cache_level = old_level

    @property
    def is_caching_enabled(self):
        return self._cache_level > 0

    def _increment_cache_level(self):
        self._cache_level += 1

    def _decrement_cache_level(self):
        assert self._cache_level > 0
        self._cache_level -= 1
        if self._cache_level == 0: self.invalidate_all_caches()

    def create_cache_site(self, source, site_name, site_group_key):
        """Creates a CacheSite instanced, managed by this CacheManager.
        The manager keeps a weak reference to the site ; the lifetime of the
        cache is controlled by the caller
        
        The site_group_key specifies the key on which to aggregate hit / miss stats in cache_info()
        Note that a reference to site_group_key will continue to be held by the CacheManager, so take
        care to select keys that are small in size, or wouldn't be garbage collected anyway (i.e. a module-level class)"""
        import weakref
        cs = CacheSite(source=source, site_name=site_name)
        # Both this CacheManager and the cache site will reference the stats object;
        # however, our referencing the stats object will not keep the CacheSite alive 
        # This allows us to calculate aggregate cache stats in cache_info() without keeping
        # cache sites and their owning objects alive.
        stats = cs.stats
        cs_weak = weakref.ref(cs, self._on_site_unreferenced)

        self._site_weakrefs.add(cs_weak)
        self._site_stats.setdefault(site_group_key, []).append(stats)
        return cs

    def cache_info(self):
        """Returns a tuple (site group key, group size, hits, misses, uncached misses) per cache site group.
        (uncached misses refers to those misses that occurred without caching enabled (see CacheManager.is_caching_enabled)
        A cache site group is an aggregation of cache sites that are considered meaningfully related,
        with regards to performance counters.
        
        For example, though a method on a class has a cache site per _instance_, all instance sites
        of a method are joined to the same site group."""
        for site_group_key, group_stats in self._site_stats.iteritems():
            yield (site_group_key, len(group_stats), 
                   sum([stat.hits for stat in group_stats]), 
                   sum([stat.misses for stat in group_stats]),
                   sum([stat.uncached_misses for stat in group_stats]))
    
    def invalidate_all_caches(self):
        """Invalidates cache sites program-wide. This method should be called whenever the Excel COM API is used to
        modify a workbook (for example, it is called by :meth:`xl.range.Range.set`).

        Alternatively, one can use :meth:`caching_disabled`, since it invalidates caches on context entry."""
        for site in self._iter_site_refs():
            site.clear()

    def _iter_site_refs(self):
        # Iterating on _site_weakrefs is tricky, because the _on_site_unreferenced 
        # callback modifies it when a site is ready to be GC'd
        # Since iterating site refs (ex. to clear caches) may remove strong
        # site references, we must prevent modification during iteration (flag shared with
        # the callback), and clean the set (to prevent accumulation of dead weakrefs)
        old_iter_state = self._iterating_site_weakrefs
        self._iterating_site_weakrefs = True
        try:
            for site_weakref in self._site_weakrefs:
                site = site_weakref()
                if not site is None: yield site

            to_discard = set()
            for site_weakref in self._site_weakrefs:
                if site_weakref() is None: to_discard.add(site_weakref)
            self._site_weakrefs -= to_discard
        finally:
            self._iterating_site_weakrefs = False

    def _on_site_unreferenced(self, site_weakref):
        if not self._iterating_site_weakrefs:
            self._site_weakrefs.discard( site_weakref )


CacheManager = CacheManager_class() 
"""Singleton CacheManager used by all of Pyvot"""