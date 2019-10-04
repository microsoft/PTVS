// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal static class BuiltinTags {
        internal static readonly Dictionary<string, string> _nestedTags = new Dictionary<string, string>() {
            { "for", "endfor" },
            { "if", "endif" },
            { "ifequal", "endifequal" },
            { "ifnotequal", "endifnotequal" },
            { "ifchanged", "endifchanged" },
            { "autoescape", "endautoescape" },
            { "comment", "endcomment" },
            { "filter", "endfilter" },
            { "spaceless", "endspaceless" },
            { "with", "endwith" },
            { "empty", "endfor" },
            { "else", "endif" },
        };

        internal static readonly HashSet<string> _nestedEndTags = MakeNestedEndTags();

        internal static readonly HashSet<string> _nestedStartTags = MakeNestedStartTags();

        private static HashSet<string> MakeNestedEndTags() {
            HashSet<string> res = new HashSet<string>();
            foreach (var value in _nestedTags.Values) {
                res.Add(value);
            }
            return res;
        }

        private static HashSet<string> MakeNestedStartTags() {
            HashSet<string> res = new HashSet<string>();
            foreach (var key in _nestedTags.Keys) {
                res.Add(key);
            }
            return res;
        }

        public static Dictionary<string, string> MakeKnownTagsTable() {
            return new Dictionary<string, string>() {
                {"elif", "adds an additional condition to an if block"},
                {"endfor", "ends a for block"},
                {"endifequal", "ends an ifequal block"},
                {"endifnotequal", "ends an ifnotequal block"},
                {"endifchanged", "ends an ifchanged block"},
                {"endautoescape", "ends an autoescape block"},
                {"endcomment", "ends a comment block"},
                {"endfilter", "ends a filter block"},
                {"endspaceless", "ends a spaceless block"},
                {"endwith", "ends a with block"},
                {"endif", "ends an if or elif block"},
                {"include", @"Loads a template and renders it with the current context. You can pass
additional context using keyword arguments.

Example::

    {% include ""fob/some_include"" %}
    {% include ""fob/some_include"" with oar=""BAZZ!"" baz=""BING!"" %}

Use the ``only`` argument to exclude the current context when rendering
the included template::

    {% include ""fob/some_include"" only %}
    {% include ""fob/some_include"" with oar=""1"" only %}
"},
                {"block", @"Define a block that can be overridden by child templates."},
                {"extends", @"Signal that this template extends a parent template.

This tag may be used in two ways: ``{% extends ""base"" %}`` (with quotes)
uses the literal value ""base"" as the name of the parent template to extend,
or ``{% extends variable %}`` uses the value of ``variable`` as either the
name of the parent template to extend (if it evaluates to a string) or as
the parent tempate itelf (if it evaluates to a Template object).
"},
                {"cycle", @"Cycles among the given strings each time this tag is encountered.

Within a loop, cycles among the given strings each time through
the loop::

    {% for o in some_list %}
        <tr class=""{% cycle 'row1' 'row2' %}"">
            ...
        </tr>
    {% endfor %}

Outside of a loop, give the values a unique name the first time you call
it, then use that name each sucessive time through::

        <tr class=""{% cycle 'row1' 'row2' 'row3' as rowcolors %}"">...</tr>
        <tr class=""{% cycle rowcolors %}"">...</tr>
        <tr class=""{% cycle rowcolors %}"">...</tr>

You can use any number of values, separated by spaces. Commas can also
be used to separate values; if a comma is used, the cycle values are
interpreted as literal strings.

The optional flag ""silent"" can be used to prevent the cycle declaration
from returning any value::

    {% cycle 'row1' 'row2' as rowcolors silent %}{# no value here #}
    {% for o in some_list %}
        <tr class=""{% cycle rowcolors %}"">{# first value will be ""row1"" #}
            ...
        </tr>
    {% endfor %}"},
                {"comment", @"Ignores everything between ``{% comment %}`` and ``{% endcomment %}``."},
                {"autoescape", @"Force autoescape behavior for this block."},
                {"csrf_token", @""},
                {"debug", @"Outputs a whole load of debugging information, including the current
context and imported modules.

Sample usage::

    <pre>
        {% debug %}
    </pre>"},
                {"filter", @"Filters the contents of the block through variable filters.

Filters can also be piped through each other, and they can have
arguments -- just like in variable syntax.

Sample usage::

    {% filter force_escape|lower %}
        This text will be HTML-escaped, and will appear in lowercase.
    {% endfilter %}

Note that the ``escape`` and ``safe`` filters are not acceptable arguments.
Instead, use the ``autoescape`` tag to manage autoescaping for blocks of
template code.
"},
                {"firstof", @"Outputs the first variable passed that is not False, without escaping.

Outputs nothing if all the passed variables are False.

Sample usage::

    {% firstof var1 var2 var3 %}

This is equivalent to::

    {% if var1 %}
        {{ var1|safe }}
    {% else %}{% if var2 %}
        {{ var2|safe }}
    {% else %}{% if var3 %}
        {{ var3|safe }}
    {% endif %}{% endif %}{% endif %}

but obviously much cleaner!

You can also use a literal string as a fallback value in case all
passed variables are False::

    {% firstof var1 var2 var3 ""fallback value"" %}

If you want to escape the output, use a filter tag::

    {% filter force_escape %}
        {% firstof var1 var2 var3 ""fallback value"" %}
    {% endfilter %}"},
                {"for", @"Loops over each item in an array.

For example, to display a list of athletes given ``athlete_list``::

    <ul>
    {% for athlete in athlete_list %}
        <li>{{ athlete.name }}</li>
    {% endfor %}
    </ul>

You can loop over a list in reverse by using
``{% for obj in list reversed %}``.

You can also unpack multiple values from a two-dimensional array::

    {% for key,value in dict.items %}
        {{ key }}: {{ value }}
    {% endfor %}

The ``for`` tag can take an optional ``{% empty %}`` clause that will
be displayed if the given array is empty or could not be found::

    <ul>
        {% for athlete in athlete_list %}
        <li>{{ athlete.name }}</li>
        {% empty %}
        <li>Sorry, no athletes in this list.</li>
        {% endfor %}
    <ul>

The above is equivalent to -- but shorter, cleaner, and possibly faster
than -- the following::

    <ul>
        {% if althete_list %}
        {% for athlete in athlete_list %}
            <li>{{ athlete.name }}</li>
        {% endfor %}
        {% else %}
        <li>Sorry, no athletes in this list.</li>
        {% endif %}
    </ul>

The for loop sets a number of variables available within the loop:

    ==========================  ================================================
    Variable                    Description
    ==========================  ================================================
    ``forloop.counter``         The current iteration of the loop (1-indexed)
    ``forloop.counter0``        The current iteration of the loop (0-indexed)
    ``forloop.revcounter``      The number of iterations from the end of the
                                loop (1-indexed)
    ``forloop.revcounter0``     The number of iterations from the end of the
                                loop (0-indexed)
    ``forloop.first``           True if this is the first time through the loop
    ``forloop.last``            True if this is the last time through the loop
    ``forloop.parentloop``      For nested loops, this is the loop ""above"" the
                                current one
    ==========================  ================================================"},
                {"ifequal", @"Outputs the contents of the block if the two arguments equal each other.

Examples::

    {% ifequal user.id comment.user_id %}
        ...
    {% endifequal %}

    {% ifnotequal user.id comment.user_id %}
        ...
    {% else %}
        ...
    {% endifnotequal %}"},
                {"ifnotequal", @"Outputs the contents of the block if the two arguments are not equal.
    See ifequal."},
                {"if", @"The ``{% if %}`` tag evaluates a variable, and if that variable is ""true""
(i.e., exists, is not empty, and is not a false boolean value), the
contents of the block are output:

::

    {% if athlete_list %}
        Number of athletes: {{ athlete_list|count }}
    {% elif athlete_in_locker_room_list %}
        Athletes should be out of the locker room soon!
    {% else %}
        No athletes.
    {% endif %}

In the above, if ``athlete_list`` is not empty, the number of athletes will
be displayed by the ``{{ athlete_list|count }}`` variable.

As you can see, the ``if`` tag may take one or several `` {% elif %}``
clauses, as well as an ``{% else %}`` clause that will be displayed if all
previous conditions fail. These clauses are optional.

``if`` tags may use ``or``, ``and`` or ``not`` to test a number of
variables or to negate a given variable::

    {% if not athlete_list %}
        There are no athletes.
    {% endif %}

    {% if athlete_list or coach_list %}
        There are some athletes or some coaches.
    {% endif %}

    {% if athlete_list and coach_list %}
        Both atheletes and coaches are available.
    {% endif %}

    {% if not athlete_list or coach_list %}
        There are no athletes, or there are some coaches.
    {% endif %}

    {% if athlete_list and not coach_list %}
        There are some athletes and absolutely no coaches.
    {% endif %}

Comparison operators are also available, and the use of filters is also
allowed, for example::

    {% if articles|length >= 5 %}...{% endif %}

Arguments and operators _must_ have a space between them, so
``{% if 1>2 %}`` is not a valid if tag.

All supported operators are: ``or``, ``and``, ``in``, ``not in``
``==`` (or ``=``), ``!=``, ``>``, ``>=``, ``<`` and ``<=``.

Operator precedence follows Python."},
                {"ifchanged", @"Checks if a value has changed from the last iteration of a loop.

The ``{% ifchanged %}`` block tag is used within a loop. It has two
possible uses.

1. Checks its own rendered contents against its previous state and only
    displays the content if it has changed. For example, this displays a
    list of days, only displaying the month if it changes::

        <h1>Archive for {{ year }}</h1>

        {% for date in days %}
            {% ifchanged %}<h3>{{ date|date:""F"" }}</h3>{% endifchanged %}
            <a href=""{{ date|date:""M/d""|lower }}/"">{{ date|date:""j"" }}</a>
        {% endfor %}

2. If given one or more variables, check whether any variable has changed.
    For example, the following shows the date every time it changes, while
    showing the hour if either the hour or the date has changed::

        {% for date in days %}
            {% ifchanged date.date %} {{ date.date }} {% endifchanged %}
            {% ifchanged date.hour date.date %}
                {{ date.hour }}
            {% endifchanged %}
        {% endfor %}"},
                {"ssi", @"Outputs the contents of a given file into the page.

Like a simple ""include"" tag, the ``ssi`` tag includes the contents
of another file -- which must be specified using an absolute path --
in the current page::

    {% ssi /home/html/ljworld.com/includes/right_generic.html %}

If the optional ""parsed"" parameter is given, the contents of the included
file are evaluated as template code, with the current context::

    {% ssi /home/html/ljworld.com/includes/right_generic.html parsed %}"},
                {"load", @"Loads a custom template tag set.

For example, to load the template tags in
``django/templatetags/news/photos.py``::

    {% load news.photos %}

Can also be used to load an individual tag/filter from
a library::

    {% load byline from news %}
"},
                {"now", @"Displays the date, formatted according to the given string.

Uses the same format as PHP's ``date()`` function; see http://php.net/date
for all the possible values.

Sample usage::

    It is {% now ""jS F Y H:i"" %}"},
                {"regroup", @"Regroups a list of alike objects by a common attribute.

This complex tag is best illustrated by use of an example:  say that
``people`` is a list of ``Person`` objects that have ``first_name``,
``last_name``, and ``gender`` attributes, and you'd like to display a list
that looks like:

    * Male:
        * George Bush
        * Bill Clinton
    * Female:
        * Margaret Thatcher
        * Colendeeza Rice
    * Unknown:
        * Pat Smith

The following snippet of template code would accomplish this dubious task::

    {% regroup people by gender as grouped %}
    <ul>
    {% for group in grouped %}
        <li>{{ group.grouper }}
        <ul>
            {% for item in group.list %}
            <li>{{ item }}</li>
            {% endfor %}
        </ul>
    {% endfor %}
    </ul>

As you can see, ``{% regroup %}`` populates a variable with a list of
objects with ``grouper`` and ``list`` attributes.  ``grouper`` contains the
item that was grouped by; ``list`` contains the list of objects that share
that ``grouper``.  In this case, ``grouper`` would be ``Male``, ``Female``
and ``Unknown``, and ``list`` is the list of people with those genders.

Note that ``{% regroup %}`` does not work when the list to be grouped is not
sorted by the key you are grouping by!  This means that if your list of
people was not sorted by gender, you'd need to make sure it is sorted
before using it, i.e.::

    {% regroup people|dictsort:""gender"" by gender as grouped %}"},
                {"spaceless", @"Removes whitespace between HTML tags, including tab and newline characters.

    Example usage::

        {% spaceless %}
            <p>
                <a href=""fob/"">Fob</a>
            </p>
        {% endspaceless %}

    This example would return this HTML::

        <p><a href=""fob/"">Fob</a></p>

    Only space between *tags* is normalized -- not space between tags and text.
    In this example, the space around ``Hello`` won't be stripped::

        {% spaceless %}
            <strong>
                Hello
            </strong>
        {% endspaceless %}"},
                {"templatetag", @"Outputs one of the bits used to compose template tags.

Since the template system has no concept of ""escaping"", to display one of
the bits used in template tags, you must use the ``{% templatetag %}`` tag.

The argument tells which template bit to output:

    ==================  =======
    Argument            Outputs
    ==================  =======
    ``openblock``       ``{%``
    ``closeblock``      ``%}``
    ``openvariable``    ``{{``
    ``closevariable``   ``}}``
    ``openbrace``       ``{``
    ``closebrace``      ``}``
    ``opencomment``     ``{#``
    ``closecomment``    ``#}``
    ==================  ======="},
                {"url", @"Returns an absolute URL matching given view with its parameters.

This is a way to define links that aren't tied to a particular URL
configuration::

    {% url path.to.some_view arg1 arg2 %}

    or

    {% url path.to.some_view name1=value1 name2=value2 %}

The first argument is a path to a view. It can be an absolute python path
or just ``app_name.view_name`` without the project name if the view is
located inside the project.  Other arguments are comma-separated values
that will be filled in place of positional and keyword arguments in the
URL. All arguments for the URL should be present.

For example if you have a view ``app_name.client`` taking client's id and
the corresponding line in a URLconf looks like this::

    ('^client/(\d+)/$', 'app_name.client')

and this app's URLconf is included into the project's URLconf under some
path::

    ('^clients/', include('project_name.app_name.urls'))

then in a template you can create a link for a certain client like this::

    {% url app_name.client client.id %}

The URL will look like ``/clients/client/123/``."},
                {"widthratio", @"For creating oar charts and such, this tag calculates the ratio of a given
value to a maximum value, and then applies that ratio to a constant.

For example::

    <img src='oar.gif' height='10' width='{% widthratio this_value max_value 100 %}' />

Above, if ``this_value`` is 175 and ``max_value`` is 200, the image in
the above example will be 88 pixels wide (because 175/200 = .875;
.875 * 100 = 87.5 which is rounded up to 88)."},
                {"with", @"Adds one or more values to the context (inside of this block) for caching
and easy access.

For example::

    {% with total=person.some_sql_method %}
        {{ total }} object{{ total|pluralize }}
    {% endwith %}

Multiple values can be added to the context::

    {% with fob=1 oar=2 %}
        ...
    {% endwith %}

The legacy format of ``{% with person.some_sql_method as total %}`` is
still accepted."},
                {"cache", @"This will cache the contents of a template fragment for a given amount
of time.

Usage::

    {% load cache %}
    {% cache [expire_time] [fragment_name] %}
        .. some expensive processing ..
    {% endcache %}

This tag also supports varying by a list of arguments::

    {% load cache %}
    {% cache [expire_time] [fragment_name] [var1] [var2] .. %}
        .. some expensive processing ..
    {% endcache %}

Each unique set of arguments will result in a unique cache entry."},
                {"localize", @"Forces or prevents localization of values, regardless of the value of
`settings.USE_L10N`.

Sample usage::

    {% localize off %}
        var pi = {{ 3.1415 }};
    {% endlocalize %}"},
                {"localtime", @"Forces or prevents conversion of datetime objects to local time,
regardless of the value of ``settings.USE_TZ``.

Sample usage::

    {% localtime off %}{{ value_in_utc }}{% endlocaltime %}"},
                {"timezone", @"Enables a given time zone just for this block.

The ``timezone`` argument must be an instance of a ``tzinfo`` subclass, a
time zone name, or ``None``. If is it a time zone name, pytz is required.
If it is ``None``, the default time zone is used within the block.

Sample usage::

    {% timezone ""Europe/Paris"" %}
        It is {{ now }} in Paris.
    {% endtimezone %}"},
                {"get_current_timezone", @"Stores the name of the current time zone in the context.

Usage::

    {% get_current_timezone as TIME_ZONE %}

This will fetch the currently active time zone and put its name
into the ``TIME_ZONE`` context variable."},
                {"get_available_languages", @"This will store a list of available languages
in the context.

Usage::

    {% get_available_languages as languages %}
    {% for language in languages %}
    ...
    {% endfor %}

This will just pull the LANGUAGES setting from
your setting file (or the default settings) and
put it into the named variable."},
                {"get_language_info", @"This will store the language information dictionary for the given language
code in a context variable.

Usage::

    {% get_language_info for LANGUAGE_CODE as l %}
    {{ l.code }}
    {{ l.name }}
    {{ l.name_local }}
    {{ l.bidi|yesno:""bi-directional,uni-directional"" }}"},
                {"get_language_info_list", @"This will store a list of language information dictionaries for the given
language codes in a context variable. The language codes can be specified
either as a list of strings or a settings.LANGUAGES style tuple (or any
sequence of sequences whose first items are language codes).

Usage::

    {% get_language_info_list for LANGUAGES as langs %}
    {% for l in langs %}
        {{ l.code }}
        {{ l.name }}
        {{ l.name_local }}
        {{ l.bidi|yesno:""bi-directional,uni-directional"" }}
    {% endfor %}"},
                {"get_current_language", @"This will store the current language in the context.

Usage::

    {% get_current_language as language %}

This will fetch the currently active language and
put it's value into the ``language`` context
variable."},
                {"get_current_language_bidi", @"This will store the current language layout in the context.

Usage::

    {% get_current_language_bidi as bidi %}

This will fetch the currently active language's layout and
put it's value into the ``bidi`` context variable.
True indicates right-to-left layout, otherwise left-to-right"},
                {"blocktrans", @"This will translate a block of text with parameters.

Usage::

    {% blocktrans with oar=fob|filter boo=baz|filter %}
    This is {{ oar }} and {{ boo }}.
    {% endblocktrans %}

Additionally, this supports pluralization::

    {% blocktrans count count=var|length %}
    There is {{ count }} object.
    {% plural %}
    There are {{ count }} objects.
    {% endblocktrans %}

This is much like ngettext, only in template syntax.

The ""var as value"" legacy format is still supported::

    {% blocktrans with fob|filter as oar and baz|filter as boo %}
    {% blocktrans count var|length as count %}

Contextual translations are supported::

    {% blocktrans with oar=fob|filter context ""greeting"" %}
        This is {{ oar }}.
    {% endblocktrans %}

This is equivalent to calling pgettext/npgettext instead of
(u)gettext/(u)ngettext."},
                {"trans", @"This will mark a string for translation and will
translate the string for the current language.

Usage::

    {% trans ""this is a test"" %}

This will mark the string for translation so it will
be pulled out by mark-messages.py into the .po files
and will run the string through the translation engine.

There is a second form::

    {% trans ""this is a test"" noop %}

This will only mark for translation, but will return
the string unchanged. Use it when you need to store
values into forms that should be translated later on.

You can use variables instead of constant strings
to translate stuff you marked somewhere else::

    {% trans variable %}

This will just try to translate the contents of
the variable ``variable``. Make sure that the string
in there is something that is in the .po file.

It is possible to store the translated string into a variable::

    {% trans ""this is a test"" as var %}
    {{ var }}

Contextual translations are also supported::

    {% trans ""this is a test"" context ""greeting"" %}

This is equivalent to calling pgettext instead of (u)gettext."},
                {"language", @"This will enable the given language just for this block.

Usage::

    {% language ""de"" %}
        This is {{ oar }} and {{ boo }}.
    {% endlanguage %}"},
                {"get_admin_log", @"Populates a template variable with the admin log for the given criteria.

Usage::

    {% get_admin_log [limit] as [varname] for_user [context_var_containing_user_obj] %}

Examples::

    {% get_admin_log 10 as admin_log for_user 23 %}
    {% get_admin_log 10 as admin_log for_user user %}
    {% get_admin_log 10 as admin_log %}

Note that ``context_var_containing_user_obj`` can be a hard-coded integer
(user ID) or the name of a template context variable containing the user
object whose ID you want."},
                {"get_comment_count", @"Gets the comment count for the given params and populates the template
context with a variable containing that value, whose name is defined by the
'as' clause.

Syntax::

    {% get_comment_count for [object] as [varname]  %}
    {% get_comment_count for [app].[model] [object_id] as [varname]  %}

Example usage::

    {% get_comment_count for event as comment_count %}
    {% get_comment_count for calendar.event event.id as comment_count %}
    {% get_comment_count for calendar.event 17 as comment_count %}"},
                {"get_comment_list", @"Gets the list of comments for the given params and populates the template
context with a variable containing that value, whose name is defined by the
'as' clause.

Syntax::

    {% get_comment_list for [object] as [varname]  %}
    {% get_comment_list for [app].[model] [object_id] as [varname]  %}

Example usage::

    {% get_comment_list for event as comment_list %}
    {% for comment in comment_list %}
        ...
    {% endfor %}"},
                {"render_comment_list", @"Render the comment list (as returned by ``{% get_comment_list %}``)
    through the ``comments/list.html`` template

    Syntax::

        {% render_comment_list for [object] %}
        {% render_comment_list for [app].[model] [object_id] %}

    Example usage::

        {% render_comment_list for event %}"},
                {"get_comment_form", @"Get a (new) form object to post a new comment.

    Syntax::

        {% get_comment_form for [object] as [varname] %}
        {% get_comment_form for [app].[model] [object_id] as [varname] %}"},
                {"render_comment_form", @"Render the comment form (as returned by ``{% render_comment_form %}``) through
    the ``comments/form.html`` template.

    Syntax::

        {% render_comment_form for [object] %}
        {% render_comment_form for [app].[model] [object_id] %}"},
                {"get_flatpages", @"Retrieves all flatpage objects available for the current site and
visible to the specific user (or visible to all users if no user is
specified). Populates the template context with them in a variable
whose name is defined by the ``as`` clause.

An optional ``for`` clause can be used to control the user whose
permissions are to be used in determining which flatpages are visible.

An optional argument, ``starts_with``, can be applied to limit the
returned flatpages to those beginning with a particular base URL.
This argument can be passed as a variable or a string, as it resolves
from the template context.

Syntax::

    {% get_flatpages ['url_starts_with'] [for user] as context_name %}

Example usage::

    {% get_flatpages as flatpages %}
    {% get_flatpages for someuser as flatpages %}
    {% get_flatpages '/about/' as about_pages %}
    {% get_flatpages prefix as about_pages %}
    {% get_flatpages '/about/' for someuser as about_pages %}"},
                {"lorem", @"Creates random Latin text useful for providing test data in templates.

Usage format::

    {% lorem [count] [method] [random] %}

``count`` is a number (or variable) containing the number of paragraphs or
words to generate (default is 1).

``method`` is either ``w`` for words, ``p`` for HTML paragraphs, ``b`` for
plain-text paragraph blocks (default is ``b``).

``random`` is the word ``random``, which if given, does not use the common
paragraph (starting ""Lorem ipsum dolor sit amet, consectetuer..."").

Examples:
    * ``{% lorem %}`` will output the common ""lorem ipsum"" paragraph
    * ``{% lorem 3 p %}`` will output the common ""lorem ipsum"" paragraph
        and two random paragraphs each wrapped in HTML ``<p>`` tags
    * ``{% lorem 2 w random %}`` will output two random latin words"},
                {"get_static_prefix", @"Populates a template variable with the static prefix,
``settings.STATIC_URL``.

Usage::

    {% get_static_prefix [as varname] %}

Examples::

    {% get_static_prefix %}
    {% get_static_prefix as static_prefix %}"},
                {"get_media_prefix", @"Populates a template variable with the media prefix,
    ``settings.MEDIA_URL``.

    Usage::

        {% get_media_prefix [as varname] %}

    Examples::

        {% get_media_prefix %}
        {% get_media_prefix as media_prefix %}"}
            };
        }
    }
}
