 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 # copy of the license can be found in the License.html file at the root of this distribution. If 
 # you cannot locate the Apache License, Version 2.0, please send an email to 
 # vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 # by the terms of the Apache License, Version 2.0.
 #
 # You must not remove this notice, or any other, from this software.
 #
 # ###########################################################################

from optparse import OptionParser
from ptvsd.attach_server import PTVS_VER, DEFAULT_PORT, enable_attach

parser = OptionParser(prog = 'ptvsd', usage = 'Usage: %prog [<option>]... <file>', version = '%prog ' + PTVS_VER)
parser.add_option('-s', '--secret', metavar = '<secret>', help = 'restrict server to only allow clients that specify <secret> when connecting')
parser.add_option('-i', '--interface', default = '0.0.0.0', metavar = '<ip-address>', help = 'listen for debugger connections on interface <ip-address>')
parser.add_option('-p', '--port', type='int', default = DEFAULT_PORT, metavar = '<port>', help = 'listen for debugger connections on <port>')
parser.add_option('--certfile', metavar = '<file>', help = 'Enable SSL and use PEM certificate from <file> to secure connection')
parser.add_option('--keyfile', metavar = '<file>', help = 'Use private key from <file> to secure connection (requires --certfile)')
parser.add_option('--no-output-redirection', dest = 'redirect_output', action = 'store_false', default = True, help = 'do not redirect stdout and stderr to debugger')

(opts, args) = parser.parse_args()
if len(args) == 0:
    parser.error('<file> not specified')
if opts.keyfile and not opts.certfile:
    parser.error('--keyfile requires --certfile')

enable_attach(opts.secret, (opts.interface, opts.port), opts.certfile, opts.keyfile, opts.redirect_output)
execfile(args[0], {})
