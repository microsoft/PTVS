# Copyright (c) Microsoft Corporation. 
#
# This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
# copy of the license can be found in the LICENSE.txt file at the root of this distribution. If 
# you cannot locate the Apache License, Version 2.0, please send an email to 
# vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
# by the terms of the Apache License, Version 2.0.
#
# You must not remove this notice, or any other, from this software.

import win32com.client as win32
import pythoncom
import pywintypes
from pywintypes import com_error
import winerror
from win32com.client import constants
import datetime

import sys
# Needed to handle some breaking pywintypes changes
_running_python3 = sys.version_info.major > 2

def ensure_excel_dispatch_support():
    """Ensure that early-bound dispatch support is generated for Excel typelib, version 1.7
    
    This may attempt to write to the site-packages directory"""
    try:
        win32.gencache.EnsureModule('{00020813-0000-0000-C000-000000000046}', 0, 1, 7)
    except Exception as e:
        raise Exception("Failed to verify / generate Excel COM wrappers. Check that you have write access to site-packages." + \
                        "See the original exception (in args[1]) for more info", e)

def marshal_to_excel_value(v):
    assert not (isinstance(v, list) or isinstance(v, tuple)), "marshal_to_excel_value only handles scalars" 

    if isinstance(v, datetime.datetime):
        return _datetime_to_com_time(v)
    else:
        return v

_com_time_type = type(pywintypes.Time(0))
def unmarshal_from_excel_value(v):
    assert not (isinstance(v, list) or isinstance(v, tuple)), "unmarshal_from_excel_value only handles scalars" 
    
    if isinstance(v, _com_time_type):
        return _com_time_to_datetime(v)
    else:
        return v

def _com_time_to_datetime(pytime):
    if _running_python3:
        # The py3 version of pywintypes has its time type inherit from datetime.
        # We copy to a new datetime so that the returned type is the same between 2/3
        # pywintypes promises to only return instances set to UTC; see doc link in _datetime_to_com_time
        assert pytime.tzinfo is not None
        return datetime.datetime(month=pytime.month, day=pytime.day, year=pytime.year, 
                                 hour=pytime.hour, minute=pytime.minute, second=pytime.second,
                                 microsecond=pytime.microsecond, tzinfo=pytime.tzinfo)
    else:
        assert pytime.msec == 0, "fractional seconds not yet handled"
        return datetime.datetime(month=pytime.month, day=pytime.day, year=pytime.year, 
                                 hour=pytime.hour, minute=pytime.minute, second=pytime.second)

def _datetime_to_com_time(dt):
    if _running_python3:
        # The py3 version of pywintypes has its time type inherit from datetime.
        # For some reason, though it accepts plain datetimes, they must have a timezone set.
        # See http://docs.activestate.com/activepython/2.7/pywin32/html/win32/help/py3k.html
        # We replace no timezone -> UTC to allow round-trips in the naive case
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=datetime.timezone.utc)
        return dt
    else:
        assert dt.microsecond == 0, "fractional seconds not yet handled"
        return pywintypes.Time( dt.timetuple() )


def enum_running_monikers():
    try:
        r = pythoncom.GetRunningObjectTable()
        for moniker in r:
            yield moniker
    except com_error as e:
        if e.args[0] == winerror.E_ACCESSDENIED:
            raise Exception("Access to the running object table was denied. This may be due to a high-privilege registered object")

# Searches the running object tablef or the workbook by filename
# None if not found
def get_running_xlWorkbook_for_filename(filename):
    # If we
    wbPartialMatch = None
    filename = filename.lower()
    context = pythoncom.CreateBindCtx(0)
    for moniker in enum_running_monikers():
        name = moniker.GetDisplayName(context, None).lower()      
        # name will be either a temp name "book1" or a full filename  "c:\temp\foo.xlsx"
        # use moniker.GetClassID() to narrow it down to a file Monikor?
        # match on full path, case insensitive
        if (filename == name):
            obj = pythoncom.GetRunningObjectTable().GetObject(moniker)
            wb = win32.Dispatch(obj.QueryInterface(pythoncom.IID_IDispatch))
            return wb
        # check for a partial match 
        if name.endswith('\\' + filename):
            obj = pythoncom.GetRunningObjectTable().GetObject(moniker)
            wbPartialMatch = win32.Dispatch(obj.QueryInterface(pythoncom.IID_IDispatch))
    
    # Didn't find a full match. Return partial match if we found one. 
    return wbPartialMatch

# Opens the workbook on disk.
def open_xlWorkbook(filename):
    excel = win32.gencache.EnsureDispatch('Excel.Application')
    excel.Visible = True
    return excel.Workbooks.Open(filename)

def get_open_xlWorkbooks():
    IID_Workbook = pythoncom.pywintypes.IID("{000208DA-0000-0000-C000-000000000046}")
    l = []
    for moniker in enum_running_monikers():
        obj = pythoncom.GetRunningObjectTable().GetObject(moniker)
        try:
            wb = win32.Dispatch(obj.QueryInterface(pythoncom.IID_IDispatch))
            # Python COM doesn't support QI for arbitrary interfaces, so we can't
            # just QI for IID_Workbook({000208DA-0000-0000-C000-000000000046})
            if (getattr(wb, "CLSID", None) == IID_Workbook):                
                l.append(wb)
        except com_error:
            pass
    return l