import abc
import os

import six


@six.add_metaclass(abc.ABCMeta)
class AbstractBaseFormat():

    @staticmethod
    @abc.abstractmethod
    def extract(fn, dest_dir, **kw):
        raise NotImplementedError

    @staticmethod
    @abc.abstractmethod
    def create(prefix, file_list, out_fn, out_folder=os.getcwd(), **kw):
        raise NotImplementedError

    @staticmethod
    @abc.abstractmethod
    def get_pkg_details(in_file):
        raise NotImplementedError
