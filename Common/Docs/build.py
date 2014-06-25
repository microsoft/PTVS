#! python3
import argparse
import os
import sys

import update_html
import upload_to_codeplex

def parse_options(args):
    parser = argparse.ArgumentParser(description='Converts and uploads documentation to CodePlex')
    parser.add_argument('--user', '-u', metavar='USERNAME', type=str,
                        help='Your CodePlex username')
    parser.add_argument('--password', '-p', metavar='PASSWORD', type=str,
                        help='Your CodePlex password')
    parser.add_argument('--dir', '-d', metavar='DIRECTORY', type=str,
                        help='Folder to upload')
    parser.add_argument('--site', metavar='SITE', type=str,
                        help='CodePlex site name. Ex: pytools or nodejstools')
    parser.add_argument('--doc-root', metavar='DOC_ROOT', type=str,
                        help='Docs folder relative path from location of ' + 
                             'build.root file. Ex: Python\\Docs')
    parser.add_argument('--dry-run', action='store_true',
                        help='If specified, does not modify CodePlex.' +
                             'Username and password are still required.')
    parser.add_argument('--convert', action='store_true',
                        help='If specified, converts .md files to .html.')
    parser.add_argument('--upload', action='store_true',
                        help='If specified, uploads files to CodePlex.' +
                             'Can be used with --dry-run option.')
    parser.add_argument('--list-outputs-only', action='store_true',
                        help='If specified in conjunction with --convert, output is a list of ' +
                             'files that will be generated once --convert is run, one per line. ' +
                             'No files are actually generated.')
    options = parser.parse_args(args)
    return options

def query_missing_options(options):
    if not options.dir:
        options.dir = os.getcwd()
    if not options.convert and not options.upload and not options.dry_run:
        resp = input('Convert to HTML? (yes/y/no/n): ').strip().lower()
        options.convert = resp in ['yes', 'y']
        resp = input('Upload to CodePlex? (yes/y/no/n/dry/d): ').strip().lower()
        options.upload = resp in ['yes', 'y', 'dry', 'd']
        options.dry_run = resp in ['dry', 'd']
    if not options.doc_root:
        options.doc_root = input('Enter DOC_ROOT: ')
    if not options.site:
        options.site = input('Enter SITE: ')
    if options.upload and not options.user:
        options.user = input('Enter USER: ')
    if options.upload and not options.password:
        options.password = input('Enter PASSWORD: ')

    return options

if __name__ == '__main__':
    options = query_missing_options(parse_options(sys.argv[1:]))
    if options.convert:
        exit_code = update_html.main(options.dir,
                                     options.site,
                                     options.doc_root,
                                     options.list_outputs_only)
        if exit_code:
            sys.exit(exit_code)
    if options.upload:
        exit_code = upload_to_codeplex.main(options.dir,
                                            options.site,
                                            options.user,
                                            options.password,
                                            options.dry_run)
        if exit_code:
            sys.exit(exit_code)
