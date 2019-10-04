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
    internal static class BuiltinFilters{
        public static Dictionary<string, string> MakeKnownFiltersTable() {
            return new Dictionary<string, string>() {
                { "dictsort", @"Takes a list of dicts, returns that list sorted by the property given in the argument." },
                { "dictsortreversed", @"Takes a list of dicts, returns that list sorted in reverse order by the property given in the argument." },
                { "addslashes", @"Adds slashes before quotes. Useful for escaping strings in CSV, for
example. Less useful for escaping JavaScript; use the ``escapejs``
filter instead.
" },
                { "capfirst", @"Capitalizes the first character of the value." },
                { "escapejs", @"Hex encodes characters for use in JavaScript strings." },
                { "fix_ampersands", @"Replaces ampersands with ``&amp;`` entities." },
                { "floatformat", @"Displays a float to a specified number of decimal places.

If called without an argument, it displays the floating point number with
one decimal place -- but only if there's a decimal place to be displayed:

* num1 = 34.23234
* num2 = 34.00000
* num3 = 34.26000
* {{ num1|floatformat }} displays ""34.2""
* {{ num2|floatformat }} displays ""34""
* {{ num3|floatformat }} displays ""34.3""

If arg is positive, it will always display exactly arg number of decimal
places:

* {{ num1|floatformat:3 }} displays ""34.232""
* {{ num2|floatformat:3 }} displays ""34.000""
* {{ num3|floatformat:3 }} displays ""34.260""

If arg is negative, it will display arg number of decimal places -- but
only if there are places to be displayed:

* {{ num1|floatformat:""-3"" }} displays ""34.232""
* {{ num2|floatformat:""-3"" }} displays ""34""
* {{ num3|floatformat:""-3"" }} displays ""34.260""

If the input float is infinity or NaN, the (platform-dependent) string
representation of that value will be displayed." },
                { "iriencode", @"Escapes an IRI value for use in a URL." },
                {"linenumbers", @"Displays text with line numbers."},
                {"lower", @"Converts a string into all lowercase."},
                {"make_list", @"Returns the value turned into a list.

For an integer, it's a list of digits.
For a string, it's a list of characters.
"},
                {"slugify", @"Normalizes string, converts to lowercase, removes non-alpha characters,
and converts spaces to hyphens.
"},
                {"stringformat", @"Formats the variable according to the arg, a string formatting specifier.

This specifier uses Python string formating syntax, with the exception that
the leading ""%"" is dropped.

See http://docs.python.org/lib/typesseq-strings.html for documentation
of Python string formatting
"},
                {"title", @"Converts a string into titlecase."},
                {"truncatechars", @"Truncates a string after a certain number of characters.

Argument: Number of characters to truncate after.
"},
                {"truncatewords", @"Truncates a string after a certain number of words.

Argument: Number of words to truncate after.

Newlines within the string are removed.
"},
                {"truncatewords_html", @"Truncates HTML after a certain number of words.

Argument: Number of words to truncate after.

Newlines in the HTML are preserved.
"},
                {"upper", @"Converts a string into all uppercase."},
                {"urlencode", @"Escapes a value for use in a URL.

Takes an optional ``safe`` parameter used to determine the characters which
should not be escaped by Django's ``urlquote`` method. If not provided, the
default safe characters will be used (but an empty string can be provided
when *all* characters should be escaped).
"},
                {"urlize", @"Converts URLs in plain text into clickable links."},
                {"urlizetrunc", @"Converts URLs into clickable links, truncating URLs to the given character
limit, and adding 'rel=nofollow' attribute to discourage spamming.

Argument: Length to truncate URLs to.
"},
                {"wordcount", @"Returns the number of words."},
                {"wordwrap", @"Wraps words at specified line length.

Argument: number of characters to wrap the text at.
"},
                {"ljust", @"Left-aligns the value in a field of a given width.

Argument: field size.
"},
                {"rjust", @"Right-aligns the value in a field of a given width.

Argument: field size.
"},
                {"center", @"Centers the value in a field of a given width."},
                {"cut", @"Removes all values of arg from the given string."},
                {"escape_filter", @"Marks the value as a string that should not be auto-escaped."},
                {"force_escape", @"Escapes a string's HTML. This returns a new string containing the escaped
characters (as opposed to ""escape"", which marks the content for later
possible escaping).
"},
                {"linebreaks_filter", @"Replaces line breaks in plain text with appropriate HTML; a single
newline becomes an HTML line break (``<br />``) and a new line
followed by a blank line becomes a paragraph break (``</p>``).
"},
                {"linebreaksbr", @"    Converts all newlines in a piece of plain text to HTML line breaks (``<br />``)."},
                {"safe", @"Marks the value as a string that should not be auto-escaped."},
                {"safeseq", @"A ""safe"" filter for sequences. Marks each element in the sequence,
individually, as safe, after converting them to unicode. Returns a list
with the results.
"},
                {"removetags", @"Removes a space separated list of [X]HTML tags from the output."},
                {"striptags", @"Strips all [X]HTML tags."},
                {"first", @"Returns the first item in a list."},
                {"join", @"Joins a list with a string, like Python's ``str.join(list)``."},
                {"last", @"Returns the last item in a list"},
                {"length", @"Returns the length of the value - useful for lists."},
                {"length_is", @"Returns a boolean of whether the value's length is the argument."},
                {"random", @"Returns a random item from the list."},
                {"slice_filter", @"Returns a slice of the list.

Uses the same syntax as Python's list slicing; see
http://diveintopython.org/native_data_types/lists.html#odbchelper.list.slice
for an introduction."},
                {"unordered_list", @"Recursively takes a self-nested list and returns an HTML unordered list --
WITHOUT opening and closing <ul> tags.

The list is assumed to be in the proper format. For example, if ``var``
contains: ``['States', ['Kansas', ['Lawrence', 'Topeka'], 'Illinois']]``,
then ``{{ var|unordered_list }}`` would return::

    <li>States
    <ul>
            <li>Kansas
            <ul>
                    <li>Lawrence</li>
                    <li>Topeka</li>
            </ul>
            </li>
            <li>Illinois</li>
    </ul>
    </li>"},
                {"add", @"Adds the arg to the value."},
                {"get_digit", @"Given a whole number, returns the requested digit of it, where 1 is the
right-most digit, 2 is the second-right-most digit, etc. Returns the
original value for invalid input (if input or argument is not an integer,
or if argument is less than 1). Otherwise, output is always an integer.
"},
                {"date", @"Formats a date according to the given format."},
                {"time", @"Formats a time according to the given format."},
                {"timesince", @"Formats a date as the time since that date (i.e. ""4 days, 6 hours"")."},
                {"timeuntil", @"Formats a date as the time until that date (i.e. ""4 days, 6 hours"")."},
                {"default", @"If value is unavailable, use given default."},
                {"default_if_none", @"If value is None, use given default."},
                {"divibleby", @"Returns True if the value is devisible by the argument."},
                {"yesno", @"Given a string mapping values for true, false and (optionally) None,
returns one of those strings according to the value:

==========  ======================  ==================================
Value       Argument                Outputs
==========  ======================  ==================================
``True``    ``""yeah,no,maybe""``     ``yeah``
``False``   ``""yeah,no,maybe""``     ``no``
``None``    ``""yeah,no,maybe""``     ``maybe``
``None``    ``""yeah,no""``           ``""no""`` (converts None to False
                                    if no mapping for None is given.
==========  ======================  ==================================
"},
                {"filesizeformat", @"Formats the value like a 'human-readable' file size (i.e. 13 KB, 4.1 MB, 102 bytes, etc)."},
                {"pluralize", @"Returns a plural suffix if the value is not 1. By default, 's' is used as
the suffix:

* If value is 0, vote{{ value|pluralize }} displays ""0 votes"".
* If value is 1, vote{{ value|pluralize }} displays ""1 vote"".
* If value is 2, vote{{ value|pluralize }} displays ""2 votes"".

If an argument is provided, that string is used instead:

* If value is 0, class{{ value|pluralize:""es"" }} displays ""0 classes"".
* If value is 1, class{{ value|pluralize:""es"" }} displays ""1 class"".
* If value is 2, class{{ value|pluralize:""es"" }} displays ""2 classes"".

If the provided argument contains a comma, the text before the comma is
used for the singular case and the text after the comma is used for the
plural case:

* If value is 0, cand{{ value|pluralize:""y,ies"" }} displays ""0 candies"".
* If value is 1, cand{{ value|pluralize:""y,ies"" }} displays ""1 candy"".
* If value is 2, cand{{ value|pluralize:""y,ies"" }} displays ""2 candies"".
"},
                {"phone2numeric", @"Takes a phone number and converts it in to its numerical equivalent."},
                {"pprint", @"A wrapper around pprint.pprint -- for debugging, really."},
                {"language_name", @""},
                {"language_name_local", @""},
                {"language_bidi", @""},
                {"localize", @"Forces a value to be rendered as a localized value, regardless of the value of ``settings.USE_L10N``."},
                {"unlocalize", @"Forces a value to be rendered as a non-localized value, regardless of the value of ``settings.USE_L10N``."},
                {"localtime", @"Converts a datetime to local time in the active time zone.

This only makes sense within a {% localtime off %} block."},
                {"utc", @"Converts a datetime to UTC."},
                {"do_timezone", @"Converts a datetime to local time in a given time zone.

The argument must be an instance of a tzinfo subclass or a time zone name.
If it is a time zone name, pytz is required.

Naive datetimes are assumed to be in local time in the default time zone."},
                {"admin_urlname", @""},
                {"cell_count", @"Returns the number of cells used in a tabular inline"},
                {"ordinal", @"Converts an integer to its ordinal as a string. 1 is '1st', 2 is '2nd',
3 is '3rd', etc. Works for any integer."},
                {"intcomma", @"Converts an integer to a string containing commas every three digits.
For example, 3000 becomes '3,000' and 45000 becomes '45,000'."},
                {"intword", @"Converts a large integer to a friendly text representation. Works best
for numbers over 1 million. For example, 1000000 becomes '1.0 million',
1200000 becomes '1.2 million' and '1200000000' becomes '1.2 billion'."},
                {"apnumber", @"For numbers 1-9, returns the number spelled out. Otherwise, returns the
number. This follows Associated Press style."},
                {"naturalday", @"For date values that are tomorrow, today or yesterday compared to
present day returns representing string. Otherwise, returns a string
formatted according to settings.DATE_FORMAT."},
                {"naturaltime", @"For date and time values shows how many seconds, minutes or hours ago
compared to current timestamp returns representing string."},
                {"textile", @""},
                {"markdown", @"Runs Markdown over a given value, optionally using various
extensions python-markdown supports.

Syntax::

    {{ value|markdown:""extension1_name,extension2_name..."" }}

To enable safe mode, which strips raw HTML and only returns HTML
generated by actual Markdown syntax, pass ""safe"" as the first
extension in the list.

If the version of Markdown in use does not support extensions,
they will be silently ignored.
"},
                {"restructuredtext", @""},
            };
        }
    }
}
