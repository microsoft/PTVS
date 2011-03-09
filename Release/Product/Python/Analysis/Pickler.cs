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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Implements a subset of the Python pickling protocol for saving out intellisense databases.  Supports dictionary of str, object,
    /// object arrays, and list of object as well as primitive types.
    /// </summary>
    public class Pickler {
        private const char LowestPrintableChar = (char)32;
        private const char HighestPrintableChar = (char)126;
        private const string Newline = "\n";
        // max elements that can be set/appended at a time using SETITEMS/APPENDS

        private delegate void PickleFunction(Pickler/*!*/ pickler, object value);
        private static readonly Dictionary<Type, PickleFunction> _dispatchTable;

        private const int _batchSize = 1000;
        private FileOutput _file;
        private Dictionary<object, int> _privMemo;    // internal fast memo which we can use if the user doesn't access memo

        static Pickler() {
            _dispatchTable = new Dictionary<Type, PickleFunction>();
            _dispatchTable[typeof(Dictionary<string, object>)] = SaveDict;
            _dispatchTable[typeof(object[])] = SaveTuple;
            _dispatchTable[typeof(List<object>)] = SaveList;
        }

        #region Public API

        public Pickler(FileStream output) {
            _file = new FileOutput(output);
            _privMemo = new Dictionary<object, int>(256, ReferenceEqualityComparer.Instance);
        }

        public void Dump(object obj) {
            WriteProto();
            Save(obj);
            Write(Opcode.Stop);
        }

        private void Memoize(object obj) {
            if (!_privMemo.ContainsKey(obj)) {
                _privMemo[obj] = _privMemo.Count;
            }
        }

        private int MemoizeNew(object obj) {
            int res;
            Debug.Assert(!_privMemo.ContainsKey(obj));
            _privMemo[obj] = res = _privMemo.Count;
            return res;
        }

        private bool MemoContains(object obj) {
            return _privMemo.ContainsKey(obj);
        }

        private bool TryWriteFastGet(object obj) {
            int value;
            if (_privMemo.TryGetValue(obj, out value)) {
                WriteGetOrPut(true, value);
                return true;
            }
            return false;
        }

        #endregion

        #region Save functions

        private void Save(object obj) {
            PickleFunction pickleFunction;
            // several typees are never memoized, check for these first.
            if (obj == null) {
                SaveNone(this, obj);
            } else if (obj is int) {
                SaveInteger(this, obj);
            } else if (obj is BigInteger) {
                SaveLong(this, obj);
            } else if (obj is bool) {
                SaveBoolean(this, obj);
            } else if (obj is double) {
                SaveFloat(this, obj);
            } else if (!TryWriteFastGet(obj)) {
                if (obj is string) {
                    // strings are common, specialize them.
                    SaveUnicode(this, obj);
                } else {
                    if (!_dispatchTable.TryGetValue(obj.GetType(), out pickleFunction)) {
                        throw new InvalidOperationException(String.Format("Unsavable type: {0}", obj.GetType()));
                    }
                    pickleFunction(this, obj);
                }
            }
        }

        private static void SaveBoolean(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj.GetType() == typeof(bool), "arg must be bool");
            if ((bool)obj) {
                pickler.Write(Opcode.NewTrue);
            } else {
                pickler.Write(Opcode.NewFalse);
            }
        }

        private static void SaveDict(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj.GetType() == typeof(Dictionary<string, object>), "arg must be dict");
            Debug.Assert(!pickler.MemoContains(obj));

            int index = pickler.MemoizeNew(obj);

            pickler.Write(Opcode.EmptyDict);

            pickler.WritePut(index);

            pickler.BatchSetItems((Dictionary<string, object>)obj);
        }

        private static void SaveFloat(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj.GetType() == typeof(double), "arg must be float");

            pickler.Write(Opcode.BinFloat);
            pickler.WriteFloat64(obj);
        }

        private static void SaveInteger(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj.GetType() == typeof(int), "arg must be int");
            if (IsUInt8(obj)) {
                pickler.Write(Opcode.BinInt1);
                pickler.WriteUInt8(obj);
            } else if (IsUInt16(obj)) {
                pickler.Write(Opcode.BinInt2);
                pickler.WriteUInt16(obj);
            } else {
                pickler.Write(Opcode.BinInt);
                pickler.WriteInt32(obj);
            }
        }

        private static void SaveList(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj.GetType() == typeof(List<object>), "arg must be list");
            Debug.Assert(!pickler.MemoContains(obj));

            int index = pickler.MemoizeNew(obj);
            pickler.Write(Opcode.EmptyList);

            pickler.WritePut(index);
            pickler.BatchAppends(((IEnumerable)obj).GetEnumerator());
        }

        private static readonly BigInteger MaxInt = new BigInteger(Int32.MaxValue);
        private static readonly BigInteger MinInt = new BigInteger(Int32.MinValue);

        private static void SaveLong(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj.GetType() == typeof(BigInteger), "arg must be long");

            BigInteger bi = (BigInteger)obj;
            if (bi.IsZero) {
                pickler.Write(Opcode.Long1);
                pickler.WriteUInt8(0);
            } else if (bi <= MaxInt && bi >= MinInt) {
                pickler.Write(Opcode.Long1);
                int value = (int)bi;
                if (IsInt8(value)) {
                    pickler.WriteUInt8(1);
                    pickler._file.Write((char)(byte)value);
                } else if (IsInt16(value)) {
                    pickler.WriteUInt8(2);
                    pickler.WriteUInt8(value & 0xff);
                    pickler.WriteUInt8((value >> 8) & 0xff);
                } else {
                    pickler.WriteUInt8(4);
                    pickler.WriteInt32(value);
                }
            } else {
                byte[] dataBytes = bi.ToByteArray();
                if (dataBytes.Length < 256) {
                    pickler.Write(Opcode.Long1);
                    pickler.WriteUInt8(dataBytes.Length);
                } else {
                    pickler.Write(Opcode.Long4);
                    pickler.WriteInt32(dataBytes.Length);
                }

                foreach (byte b in dataBytes) {
                    pickler.WriteUInt8(b);
                }
            }
        }

        private static void SaveNone(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj == null, "arg must be None");
            pickler.Write(Opcode.NoneValue);
        }

        private static void SaveTuple(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj.GetType() == typeof(object[]), "arg must be array");
            Debug.Assert(!pickler.MemoContains(obj));
            object[] t = (object[])obj;
            byte opcode;
            bool needMark = false;
            int len = t.Length;
            if (len == 0) {
                opcode = Opcode.EmptyTuple;
            } else if (len == 1) {
                opcode = Opcode.Tuple1;
            } else if (len == 2) {
                opcode = Opcode.Tuple2;
            } else if (len == 3) {
                opcode = Opcode.Tuple3;
            } else {
                opcode = Opcode.Tuple;
                needMark = true;
            }

            if (needMark) pickler.Write(Opcode.Mark);
            var data = t;
            for (int i = 0; i < data.Length; i++) {
                pickler.Save(data[i]);
            }

            if (len > 0) {
                if (pickler.MemoContains(obj)) {
                    // recursive tuple
                    for (int i = 0; i < len; i++) {
                        pickler.Write(Opcode.Pop);
                    }
                    pickler.WriteGet(obj);
                    return;
                }

                pickler.Write(opcode);

                pickler.Memoize(t);
                pickler.WritePut(t);
            } else {
                pickler.Write(opcode);
            }
        }

        private static void SaveUnicode(Pickler/*!*/ pickler, object obj) {
            Debug.Assert(obj.GetType() == typeof(string), "arg must be unicode");
            Debug.Assert(!pickler.MemoContains(obj));


            var memo = pickler._privMemo[obj] = pickler._privMemo.Count;

            pickler.Write(Opcode.BinUnicode);
            pickler.WriteUnicodeStringUtf8(obj);

            pickler.WriteGetOrPut(false, memo);
        }

        #endregion

        #region Output encoding

        /// <summary>
        /// Write value in pickle decimalnl_short format.
        /// </summary>
        private void WriteFloatAsString(object value) {
            Debug.Assert(value.GetType() == typeof(double));
            Write(((double)value).ToString("R"));
            Write(Newline);
        }

        /// <summary>
        /// Write value in pickle float8 format.
        /// </summary>
        private void WriteFloat64(object value) {
            Debug.Assert(value.GetType() == typeof(double));
            foreach (var b in BitConverter.GetBytes((double)value).Reverse()) {
                Write(b);
            }
        }

        /// <summary>
        /// Write value in pickle uint1 format.
        /// </summary>
        private void WriteUInt8(object value) {
            Debug.Assert(IsUInt8(value));

            if (value is int) {
                Write((char)(int)(value));
            } else if (value is BigInteger) {
                Write((char)(int)(BigInteger)(value));
            } else if (value is byte) {
                // TODO: Shouldn't be here
                Write((char)(byte)(value));
            } else {
                throw new InvalidOperationException();
            }
        }

        private void WriteUInt8(int value) {
            _file.Write((char)value);
        }

        /// <summary>
        /// Write value in pickle uint2 format.
        /// </summary>
        private void WriteUInt16(object value) {
            Debug.Assert(IsUInt16(value));
            int iVal = (int)value;
            WriteUInt8(iVal & 0xff);
            WriteUInt8((iVal >> 8) & 0xff);
        }

        /// <summary>
        /// Write value in pickle int4 format.
        /// </summary>
        private void WriteInt32(object value) {
            int val = (int)value;
            WriteInt32(val);
        }

        private void WriteInt32(int val) {
            _file.Write(val);
        }

        /// <summary>
        /// Write value in pickle decimalnl_short format.
        /// </summary>
        private void WriteIntAsString(object value) {
            Write(value.ToString());
            Write(Newline);
        }

        /// <summary>
        /// Write value in pickle decimalnl_short format.
        /// </summary>
        private void WriteIntAsString(int value) {
            Write(value.ToString());
            Write(Newline);
        }

        /// <summary>
        /// Write value in pickle decimalnl_long format.
        /// </summary>
        private void WriteLongAsString(object value) {
            Debug.Assert(value.GetType() == typeof(BigInteger));
            Write(value.ToString());
            Write(Newline);
        }

        private static string MakeString(IList<byte> bytes) {
            return MakeString(bytes, bytes.Count);
        }

        private static string MakeString(IList<byte> bytes, int maxBytes) {
            int bytesToCopy = Math.Min(bytes.Count, maxBytes);
            StringBuilder b = new StringBuilder(bytesToCopy);
            for (int i = 0; i < bytesToCopy; i++) {
                b.Append((char)bytes[i]);
            }
            return b.ToString();
        }

        /// <summary>
        /// Write value in pickle unicodestring4 format.
        /// </summary>
        private void WriteUnicodeStringUtf8(object value) {
            Debug.Assert(value.GetType() == typeof(string));
            string strVal = (string)value;

            // if the string contains non-ASCII elements it needs to be re-encoded as UTF8.
            for (int i = 0; i < strVal.Length; i++) {
                if (strVal[i] >= 128) {
                    var encodedString = System.Text.Encoding.UTF8.GetBytes((string)value);
                    WriteInt32(encodedString.Length);
                    _file.Write(encodedString);
                    return;
                }
            }

            WriteInt32(strVal.Length);
            Write(strVal);
        }


        #endregion

        #region Type checking

        /// <summary>
        /// Return true if value is appropriate for formatting in pickle uint1 format.
        /// </summary>
        private static bool IsUInt8(object value) {
            if (value is int) {
                return IsUInt8((int)value);
            }

            throw new InvalidOperationException("expected int");
        }

        private static bool IsUInt8(int value) {
            return (value >= 0 && value < 1 << 8);
        }

        private static bool IsInt8(int value) {
            return (value >= SByte.MinValue && value <= SByte.MaxValue);
        }

        /// <summary>
        /// Return true if value is appropriate for formatting in pickle uint2 format.
        /// </summary>
        private static bool IsUInt16(object value) {
            if (value is int) {
                return IsUInt16((int)value);
            }

            throw new InvalidOperationException("expected int");
        }

        private static bool IsUInt16(int value) {
            return (value >= 0 && value < 1 << 16);
        }

        private static bool IsInt16(int value) {
            return (value >= short.MinValue && value <= short.MaxValue);
        }

        #endregion

        #region Output generation helpers

        private void Write(string data) {
            _file.Write(data);
        }

        private void Write(char data) {
            _file.Write(data);
        }

        private void Write(byte data) {
            _file.Write((char)data);
        }

        private void WriteGet(object obj) {
            Debug.Assert(MemoContains(obj));

            WriteGetOrPut(obj, true);
        }

        private void WriteGetOrPut(object obj, bool isGet) {
            Debug.Assert(MemoContains(obj));

            WriteGetOrPut(isGet, _privMemo[obj]);
        }

        private void WriteGetOrPut(bool isGet, object[] tup) {
            object index = tup[0];
            if (IsUInt8(index)) {
                Write(isGet ? Opcode.BinGet : Opcode.BinPut);
                WriteUInt8(index);
            } else {
                Write(isGet ? Opcode.LongBinGet : Opcode.LongBinPut);
                WriteInt32(index);
            }
        }

        private void WriteGetOrPut(bool isGet, int index) {
            if (index >= 0 && index <= 1 << 8) {
                Write(isGet ? Opcode.BinGet : Opcode.BinPut);
                WriteUInt8(index);
            } else {
                Write(isGet ? Opcode.LongBinGet : Opcode.LongBinPut);
                WriteInt32(index);
            }
        }

        private void WritePut(object obj) {
            WriteGetOrPut(obj, false);
        }

        private void WritePut(int index) {
            WriteGetOrPut(false, index);
        }

        private void WriteProto() {
            Write(Opcode.Proto);
            WriteUInt8(2);
        }

        /// <summary>
        /// Emit a series of opcodes that will set append all items indexed by iter
        /// to the object at the top of the stack. Use APPENDS if possible, but
        /// append no more than BatchSize items at a time.
        /// </summary>
        private void BatchAppends(IEnumerator enumerator) {
            object next;
            if (enumerator.MoveNext()) {
                next = enumerator.Current;
            } else {
                return;
            }

            int batchCompleted = 0;
            object current;

            // We do a one-item lookahead to avoid emitting an APPENDS for a
            // single remaining item.
            while (enumerator.MoveNext()) {
                current = next;
                next = enumerator.Current;

                if (batchCompleted == _batchSize) {
                    Write(Opcode.Appends);
                    batchCompleted = 0;
                }

                if (batchCompleted == 0) {
                    Write(Opcode.Mark);
                }

                Save(current);
                batchCompleted++;
            }

            if (batchCompleted == _batchSize) {
                Write(Opcode.Appends);
                batchCompleted = 0;
            }
            Save(next);
            batchCompleted++;

            if (batchCompleted > 1) {
                Write(Opcode.Appends);
            } else {
                Write(Opcode.Append);
            }

        }

        /// <summary>
        /// Emit a series of opcodes that will set all (key, value) pairs indexed by
        /// iter in the object at the top of the stack. Use SETITEMS if possible,
        /// but append no more than BatchSize items at a time.
        /// </summary>
        private void BatchSetItems(Dictionary<string, object> dict) {
            KeyValuePair<string, object> kvTuple;
            using (var enumerator = dict.GetEnumerator()) {
                object nextKey, nextValue;
                if (enumerator.MoveNext()) {
                    kvTuple = enumerator.Current;
                    nextKey = kvTuple.Key;
                    nextValue = kvTuple.Value;
                } else {
                    return;
                }

                int batchCompleted = 0;
                object curKey, curValue;

                // We do a one-item lookahead to avoid emitting a SETITEMS for a
                // single remaining item.
                while (enumerator.MoveNext()) {
                    curKey = nextKey;
                    curValue = nextValue;
                    kvTuple = enumerator.Current;
                    nextKey = kvTuple.Key;
                    nextValue = kvTuple.Value;

                    if (batchCompleted == _batchSize) {
                        Write(Opcode.SetItems);
                        batchCompleted = 0;
                    }

                    if (batchCompleted == 0) {
                        Write(Opcode.Mark);
                    }

                    Save(curKey);
                    Save(curValue);
                    batchCompleted++;
                }

                if (batchCompleted == _batchSize) {
                    Write(Opcode.SetItems);
                    batchCompleted = 0;
                }
                Save(nextKey);
                Save(nextValue);
                batchCompleted++;

                if (batchCompleted > 1) {
                    Write(Opcode.SetItems);
                } else {
                    Write(Opcode.SetItem);
                }

            }
        }

        /// <summary>
        /// Emit a series of opcodes that will set all (key, value) pairs indexed by
        /// iter in the object at the top of the stack. Use SETITEMS if possible,
        /// but append no more than BatchSize items at a time.
        /// </summary>
        private void BatchSetItems(IEnumerator enumerator) {
            object[] kvTuple;

            object nextKey, nextValue;
            if (enumerator.MoveNext()) {
                kvTuple = (object[])enumerator.Current;
                nextKey = kvTuple[0];
                nextValue = kvTuple[1];
            } else {
                return;
            }

            int batchCompleted = 0;
            object curKey, curValue;

            // We do a one-item lookahead to avoid emitting a SETITEMS for a
            // single remaining item.
            while (enumerator.MoveNext()) {
                curKey = nextKey;
                curValue = nextValue;
                kvTuple = (object[])enumerator.Current;
                nextKey = kvTuple[0];
                nextValue = kvTuple[1];

                if (batchCompleted == _batchSize) {
                    Write(Opcode.SetItems);
                    batchCompleted = 0;
                }

                if (batchCompleted == 0) {
                    Write(Opcode.Mark);
                }

                Save(curKey);
                Save(curValue);
                batchCompleted++;
            }

            if (batchCompleted == _batchSize) {
                Write(Opcode.SetItems);
                batchCompleted = 0;
            }
            Save(nextKey);
            Save(nextValue);
            batchCompleted++;

            if (batchCompleted > 1) {
                Write(Opcode.SetItems);
            } else {
                Write(Opcode.SetItem);
            }

        }

        #endregion

        #region Other private helper methods

        private Exception CannotPickle(object obj, string format, params object[] args) {
            StringBuilder msgBuilder = new StringBuilder();
            msgBuilder.Append("Can't pickle ");
            msgBuilder.Append(obj.ToString());
            if (format != null) {
                msgBuilder.Append(": ");
                msgBuilder.Append(String.Format(format, args));
            }
            throw new InvalidOperationException(msgBuilder.ToString());
        }

        /// <summary>
        /// Interface for "file-like objects" that implement the protocol needed by dump() and friends.
        /// This enables the creation of thin wrappers that make fast .NET types and slow Python types look the same.
        /// </summary>
        internal class FileOutput {
            private readonly byte[] int32chars = new byte[4];
            private readonly FileStream _writer;
            public FileOutput(FileStream writer) {
                _writer = writer;
            }

            public void Write(byte[] data) {
                _writer.Write(data, 0, data.Length);
            }

            public void Write(string data) {
                for (int i = 0; i < data.Length; i++) {
                    _writer.WriteByte((byte)data[i]);
                }
            }

            public virtual void Write(int data) {
                int32chars[0] = (byte)((data & 0xff));
                int32chars[1] = (byte)((data >> 8) & 0xff);
                int32chars[2] = (byte)((data >> 16) & 0xff);
                int32chars[3] = (byte)((data >> 24) & 0xff);
                _writer.Write(int32chars, 0, 4);
            }

            public virtual void Write(char data) {
                _writer.WriteByte((byte)data);
            }
        }

        #endregion

        class ReferenceEqualityComparer : IEqualityComparer<object> {
            public static ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            #region IEqualityComparer<object> Members

            public new bool Equals(object x, object y) {
                return x == y;
            }

            public int GetHashCode(object obj) {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }

            #endregion
        }
    }
}
