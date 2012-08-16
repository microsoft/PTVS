/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Intellisense {
    public static class Unpickle {
        /// <summary>
        /// Unpickles a Python pickle stream but returns Dictionary[object, object] for PythonDictionaries,
        /// arrays for tuples, and List[object] for Python lists.  Classes are not supported.
        /// </summary>
        /// <exception cref="System.Text.DecoderFallbackException"></exception>
        /// <exception cref="System.ArgumentException"></exception>
        /// <exception cref="System.InvalidOperationException"></exception>
        public static object Load(Stream file) {
            return new UnpicklerObject(file).Load();
        }

        /// <summary>
        /// Interface for "file-like objects" that implement the protocol needed by load() and friends.
        /// This enables the creation of thin wrappers that make fast .NET types and slow Python types look the same.
        /// </summary>
        internal class FileInput {
            private readonly Stream _stream;

            public FileInput(Stream file) {
                _stream = file;
            }

            public string Read(int size) {
                byte[] bytes = new byte[size];

                int read = _stream.Read(bytes, 0, size);
                if (read != size) {
                    throw new EndOfStreamException("end of stream while reading");
                }

                StringBuilder res = new StringBuilder(size);
                for (int i = 0; i < bytes.Length; i++) {
                    res.Append((char)bytes[i]);
                }
                return res.ToString();
            }

            public string ReadLine() {
                StringBuilder res = new StringBuilder();
                int curByte;
                do {
                    curByte = _stream.ReadByte();
                    if (curByte == -1) {
                        break;
                    }

                    if (curByte == '\r') {
                        curByte = _stream.ReadByte();
                    }
                    res.Append((char)curByte);
                } while (curByte != '\n');
                return res.ToString();
            }

            public string ReadLineNoNewLine() {
                var raw = ReadLine();
                return raw.Substring(0, raw.Length - 1);
            }

            public byte ReadChar() {
                var res = _stream.ReadByte();
                if (res == -1) {
                    throw new EndOfStreamException("unexpected EOF while unpickling");
                }
                return (byte)res;
            }

            public int ReadInt() {
                return (int)ReadChar() |
                       ((int)ReadChar()) << 8 |
                       ((int)ReadChar()) << 16 |
                       ((int)ReadChar()) << 24;
            }
        }

        #region Opcode constants

        #endregion

        class UnpicklerObject {
            private static readonly object _mark = new object();
            private FileInput _file;
            private List<object> _stack;
            private List<object> _privMemo;

            public UnpicklerObject() {
                _privMemo = new List<object>(200);
            }

            public UnpicklerObject(Stream file)
                : this() {
                _file = new FileInput(file);
            }

            public object Load() {
                _stack = new List<object>(32);

                for (; ; ) {
                    var opcode = _file.ReadChar();

                    switch (opcode) {
                        case Opcode.Append: LoadAppend(); break;
                        case Opcode.Appends: LoadAppends(); break;
                        case Opcode.BinFloat: LoadBinFloat(); break;
                        case Opcode.BinGet: LoadBinGet(); break;
                        case Opcode.BinInt: LoadBinInt(); break;
                        case Opcode.BinInt1: LoadBinInt1(); break;
                        case Opcode.BinInt2: LoadBinInt2(); break;
                        case Opcode.BinPut: LoadBinPut(); break;
                        case Opcode.BinString: LoadBinString(); break;
                        case Opcode.BinUnicode: LoadBinUnicode(); break;
                        case Opcode.Dict: LoadDict(); break;
                        case Opcode.Dup: LoadDup(); break;
                        case Opcode.EmptyDict: LoadEmptyDict(); break;
                        case Opcode.EmptyList: LoadEmptyList(); break;
                        case Opcode.EmptyTuple: LoadEmptyTuple(); break;
                        case Opcode.Float: LoadFloat(); break;
                        case Opcode.Get: LoadGet(); break;
                        case Opcode.Int: LoadInt(); break;
                        case Opcode.List: LoadList(); break;
                        case Opcode.Long: LoadLong(); break;
                        case Opcode.Long1: LoadLong1(); break;
                        case Opcode.Long4: LoadLong4(); break;
                        case Opcode.LongBinGet: LoadLongBinGet(); break;
                        case Opcode.LongBinPut: LoadLongBinPut(); break;
                        case Opcode.Mark: LoadMark(); break;
                        case Opcode.NewFalse: LoadNewFalse(); break;
                        case Opcode.NewTrue: LoadNewTrue(); break;
                        case Opcode.NoneValue: LoadNoneValue(); break;
                        case Opcode.Pop: LoadPop(); break;
                        case Opcode.PopMark: LoadPopMark(); break;
                        case Opcode.Proto: LoadProto(); break;
                        case Opcode.Put: LoadPut(); break;
                        case Opcode.SetItem: LoadSetItem(); break;
                        case Opcode.SetItems: LoadSetItems(); break;
                        case Opcode.ShortBinstring: LoadShortBinstring(); break;
                        case Opcode.String: LoadString(); break;
                        case Opcode.Tuple: LoadTuple(); break;
                        case Opcode.Tuple1: LoadTuple1(); break;
                        case Opcode.Tuple2: LoadTuple2(); break;
                        case Opcode.Tuple3: LoadTuple3(); break;
                        case Opcode.Unicode: LoadUnicode(); break;
                        case Opcode.Global: LoadGlobal(); break;
                        case Opcode.Stop: return PopStack();
                        default: throw new InvalidOperationException(String.Format("invalid opcode: {0}", opcode));
                    }
                }
            }

            private void LoadGlobal() {
                string module = ReadLineNoNewline();
                string attr = ReadLineNoNewline();
                Debug.Fail(String.Format("unexpected global in pickle stream {0}.{1}", module, attr));
                _stack.Add(null);   // no support for actually loading the globals...
            }

            private object PopStack() {
                var res = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                return res;
            }

            private object PeekStack() {
                return _stack[_stack.Count - 1];
            }

            public object[] StackGetSliceAsArray(int start) {
                object[] res = new object[_stack.Count - start];
                for (int i = 0; i < res.Length; i++) {
                    res[i] = _stack[i + start];
                }
                return res;
            }

            private object MemoGet(int key) {
                object value;

                if (key < _privMemo.Count && (value = _privMemo[key]) != _mark) {
                    return value;
                }

                throw new InvalidOperationException(String.Format("memo key {0} not found", key));
            }

            private void MemoPut(int key, object value) {
                while (key >= _privMemo.Count) {
                    _privMemo.Add(_mark);
                }
                _privMemo[key] = value;
            }

            private int GetMarkIndex() {
                int i = _stack.Count - 1;
                while (i > 0 && _stack[i] != _mark) i -= 1;
                if (i == -1) throw new InvalidOperationException("mark not found");
                return i;
            }

            private string Read(int size) {
                string res = _file.Read(size);
                if (res.Length < size) {
                    throw new EndOfStreamException("unexpected EOF while unpickling");
                }
                return res;
            }

            private string ReadLineNoNewline() {
                string raw = _file.ReadLine();
                return raw.Substring(0, raw.Length - 1);
            }

            private object ReadFloatString() {
                return Double.Parse(ReadLineNoNewline());
            }

            private double ReadFloat64() {
                byte[] bytes = new byte[8];
                for (int i = 0; i < bytes.Length; i++) {
                    bytes[i] = _file.ReadChar();
                }
                return BitConverter.ToDouble(bytes, 0);
            }

            private object ReadIntFromString() {
                string raw = ReadLineNoNewline();
                if ("00" == raw) return False;
                else if ("01" == raw) return True;
                return Int32.Parse(raw);
            }

            private int ReadInt32() {
                return _file.ReadInt();
            }

            private object ReadLongFromString() {
                var i = ReadLineNoNewline();
                if (i.EndsWith("L")) {
                    i = i.Substring(0, i.Length - 1);
                }
                return BigInteger.Parse(i);
            }

            private object ReadLong(int size) {
                byte[] bytes = new byte[size];
                for (int i = 0; i < size; i++) {
                    bytes[i] = _file.ReadChar();
                }
                return new BigInteger(bytes);
            }

            private char ReadUInt8() {
                return (char)_file.ReadChar();
            }

            private ushort ReadUInt16() {
                byte[] bytes = new byte[2];
                for (int i = 0; i < bytes.Length; i++) {
                    bytes[i] = _file.ReadChar();
                }
                return BitConverter.ToUInt16(bytes, 0);
            }

            private void PopMark(int markIndex) {
                for (int i = _stack.Count - 1; i >= markIndex; i--) {
                    _stack.RemoveAt(i);
                }
            }

            /// <summary>
            /// Interpret everything from markIndex to the top of the stack as a sequence
            /// of key, value, key, value, etc. Set dict[key] = value for each. Pop
            /// everything from markIndex up when done.
            /// </summary>
            private void SetItems(Dictionary<string, object> dict, int markIndex) {
                for (int i = markIndex + 1; i < _stack.Count; i += 2) {
                    dict[(string)_stack[i]] = _stack[i + 1];
                }
                PopMark(markIndex);
            }

            private void LoadAppend() {
                object item = PopStack();
                object seq = PeekStack();
                if (seq is List<object>) {
                    ((List<object>)seq).Add(item);
                } else {
                    throw new InvalidOperationException();
                }
            }

            private void LoadAppends() {
                int markIndex = GetMarkIndex();
                List<object> seq = (List<object>)_stack[markIndex - 1];
                for (int i = markIndex + 1; i < _stack.Count; i++) {
                    seq.Add(_stack[i]);
                }
                PopMark(markIndex);
            }

            private void LoadBinFloat() {
                _stack.Add(ReadFloat64());
            }

            private void LoadBinGet() {
                _stack.Add(MemoGet(ReadUInt8()));
            }

            private void LoadBinInt() {
                _stack.Add(ReadInt32());
            }

            private void LoadBinInt1() {
                _stack.Add((int)ReadUInt8());
            }

            private void LoadBinInt2() {
                _stack.Add((int)ReadUInt16());
            }

            private void LoadBinPut() {
                MemoPut(ReadUInt8(), PeekStack());
            }

            private void LoadBinString() {
                _stack.Add(Read(ReadInt32()));
            }

            private void LoadBinUnicode() {
                byte[] bytes = new byte[ReadInt32()];
                for (int i = 0; i < bytes.Length; i++) {
                    bytes[i] = _file.ReadChar();
                }

                _stack.Add(Encoding.UTF8.GetString(bytes));
            }

            private void LoadDict() {
                int markIndex = GetMarkIndex();
                Dictionary<string, object> dict = new Dictionary<string, object>((_stack.Count - 1 - markIndex) / 2);
                SetItems(dict, markIndex);
                _stack.Add(dict);
            }

            private void LoadDup() {
                _stack.Add(PeekStack());
            }

            private void LoadEmptyDict() {
                _stack.Add(new Dictionary<string, object>());
            }

            private void LoadEmptyList() {
                _stack.Add(new List<object>());
            }

            private void LoadEmptyTuple() {
                _stack.Add(new object[0]);
            }

            private void LoadFloat() {
                _stack.Add(ReadFloatString());
            }

            private void LoadGet() {
                _stack.Add(MemoGet((int)ReadIntFromString()));
            }

            private void LoadInt() {
                _stack.Add(ReadIntFromString());
            }

            private void LoadList() {
                int markIndex = GetMarkIndex();
                var list = new List<object>(StackGetSliceAsArray(markIndex + 1));
                PopMark(markIndex);
                _stack.Add(list);
            }

            private void LoadLong() {
                _stack.Add(ReadLongFromString());
            }

            private void LoadLong1() {
                int size = ReadUInt8();
                if (size == 4) {
                    _stack.Add((BigInteger)ReadInt32());
                } else {
                    _stack.Add(ReadLong(size));
                }
            }

            private void LoadLong4() {
                _stack.Add(ReadLong(ReadInt32()));
            }

            private void LoadLongBinGet() {
                _stack.Add(MemoGet((int)ReadInt32()));
            }

            private void LoadLongBinPut() {
                MemoPut(ReadInt32(), PeekStack());
            }

            private void LoadMark() {
                _stack.Add(_mark);
            }

            private static object False = false;
            private static object True = true;
            private void LoadNewFalse() {
                _stack.Add(False);
            }

            private void LoadNewTrue() {
                _stack.Add(True);
            }

            private void LoadNoneValue() {
                _stack.Add(null);
            }

            private void LoadPop() {
                PopStack();
            }

            private void LoadPopMark() {
                PopMark(GetMarkIndex());
            }

            private void LoadProto() {
                int proto = ReadUInt8();
                if (proto > 2) throw new ArgumentException(String.Format("unsupported pickle protocol: {0}", proto));
                // discard result
            }

            private void LoadPut() {
                MemoPut((int)ReadIntFromString(), PeekStack());
            }

            private void LoadSetItem() {
                object value = PopStack();
                object key = PopStack();
                Dictionary<string, object> dict = PeekStack() as Dictionary<string, object>;
                if (dict == null) {
                    throw new InvalidOperationException(
                        String.Format(
                            "while executing SETITEM, expected dict at stack[-3], but got {0}",
                            PeekStack()
                        )
                    );
                }
                dict[(string)key] = value;
            }

            private void LoadSetItems() {
                int markIndex = GetMarkIndex();
                Dictionary<string, object> dict = _stack[markIndex - 1] as Dictionary<string, object>;

                if (dict == null) {
                    throw new InvalidOperationException(
                        String.Format(
                            "while executing SETITEMS, expected dict below last mark, but got {0}",
                            _stack[markIndex - 1]
                        )
                    );
                }
                SetItems(dict, markIndex);
            }

            private void LoadShortBinstring() {
                _stack.Add(Read(ReadUInt8()));
            }

            private void LoadString() {
                string repr = ReadLineNoNewline();
                if (repr.Length < 2 ||
                    !(
                    repr[0] == '"' && repr[repr.Length - 1] == '"' ||
                    repr[0] == '\'' && repr[repr.Length - 1] == '\''
                    )
                ) {
                    throw new ArgumentException(String.Format("while executing STRING, expected string that starts and ends with quotes {0}", repr));
                }
                _stack.Add(LiteralParser.ParseString(repr.Substring(1, repr.Length - 2), false, false));
            }

            private void LoadTuple() {
                int markIndex = GetMarkIndex();
                var tuple = StackGetSliceAsArray(markIndex + 1);
                PopMark(markIndex);
                _stack.Add(tuple);
            }

            private void LoadTuple1() {
                object item0 = PopStack();
                _stack.Add(new[] { item0 });
            }

            private void LoadTuple2() {
                object item1 = PopStack();
                object item0 = PopStack();
                _stack.Add(new[] { item0, item1 });
            }

            private void LoadTuple3() {
                object item2 = PopStack();
                object item1 = PopStack();
                object item0 = PopStack();
                _stack.Add(new[] { item0, item1, item2 });
            }

            private void LoadUnicode() {
                _stack.Add(LiteralParser.ParseString(ReadLineNoNewline(), false, true));
            }
        }
    }


    internal static class Opcode {
        public const byte Append = (byte)'a';
        public const byte Appends = (byte)'e';
        public const byte BinFloat = (byte)'G';
        public const byte BinGet = (byte)'h';
        public const byte BinInt = (byte)'J';
        public const byte BinInt1 = (byte)'K';
        public const byte BinInt2 = (byte)'M';
        public const byte BinPersid = (byte)'Q';
        public const byte BinPut = (byte)'q';
        public const byte BinString = (byte)'T';
        public const byte BinUnicode = (byte)'X';
        public const byte Build = (byte)'b';
        public const byte Dict = (byte)'d';
        public const byte Dup = (byte)'2';
        public const byte EmptyDict = (byte)'}';
        public const byte EmptyList = (byte)']';
        public const byte EmptyTuple = (byte)')';
        public const byte Ext1 = (byte)'\x82';
        public const byte Ext2 = (byte)'\x83';
        public const byte Ext4 = (byte)'\x84';
        public const byte Float = (byte)'F';
        public const byte Get = (byte)'g';
        public const byte Global = (byte)'c';
        public const byte Inst = (byte)'i';
        public const byte Int = (byte)'I';
        public const byte List = (byte)'l';
        public const byte Long = (byte)'L';
        public const byte Long1 = (byte)'\x8a';
        public const byte Long4 = (byte)'\x8b';
        public const byte LongBinGet = (byte)'j';
        public const byte LongBinPut = (byte)'r';
        public const byte Mark = (byte)'(';
        public const byte NewFalse = (byte)'\x89';
        public const byte NewObj = (byte)'\x81';
        public const byte NewTrue = (byte)'\x88';
        public const byte NoneValue = (byte)'N';
        public const byte Obj = (byte)'o';
        public const byte PersId = (byte)'P';
        public const byte Pop = (byte)'0';
        public const byte PopMark = (byte)'1';
        public const byte Proto = (byte)'\x80';
        public const byte Put = (byte)'p';
        public const byte Reduce = (byte)'R';
        public const byte SetItem = (byte)'s';
        public const byte SetItems = (byte)'u';
        public const byte ShortBinstring = (byte)'U';
        public const byte Stop = (byte)'.';
        public const byte String = (byte)'S';
        public const byte Tuple = (byte)'t';
        public const byte Tuple1 = (byte)'\x85';
        public const byte Tuple2 = (byte)'\x86';
        public const byte Tuple3 = (byte)'\x87';
        public const byte Unicode = (byte)'V';
    }

}
