// Lightweight PE export reader with caching for safe attach scenarios.
// Shared helper to resolve export symbol RVAs without DIA / symbol files.
// NOTE: Designed for minimal allocations and to work with partial header reads.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.PythonTools.Debugging.Shared.SafeAttach {
    internal struct ExportSymbol {
        public ulong Rva;            // RVA of symbol (as reported in export table)
        public bool LooksLikeFunction; // Heuristic classification
    }

    internal static class PeExportReader {
        private const int HEADER_READ = 0x800; // same as orchestrator (enough for PE header + data dirs)
        private const uint IMAGE_DIRECTORY_ENTRY_EXPORT = 0;

        private class CacheEntry {
            public Dictionary<string, ExportSymbol> Symbols = new Dictionary<string, ExportSymbol>(StringComparer.Ordinal);
        }

        // key: module base address
        private static readonly ConcurrentDictionary<ulong, CacheEntry> _cache = new ConcurrentDictionary<ulong, CacheEntry>();

        /// <summary>
        /// Attempts to locate an exported symbol by name. Returns false if export table or symbol not found.
        /// read delegate must read exactly size bytes from absolute address.
        /// </summary>
        internal static bool TryGetExport(Func<ulong, byte[], int, bool> read, ulong moduleBase, string exportName, out ExportSymbol symbol) {
            symbol = default;
            if (moduleBase == 0 || read == null || string.IsNullOrEmpty(exportName)) {
                return false;
            }

            if (_cache.TryGetValue(moduleBase, out var existing) && existing.Symbols.TryGetValue(exportName, out symbol)) {
                return symbol.Rva != 0;
            }

            // Populate cache lazily (single pass)
            var entry = _cache.GetOrAdd(moduleBase, _ => new CacheEntry());
            if (entry.Symbols.Count == 0) {
                try { ParseExports(read, moduleBase, entry); } catch (Exception ex) { Debug.WriteLine("[PTVS][PeExportReader] Parse failed: " + ex.Message); }
            }

            if (entry.Symbols.TryGetValue(exportName, out symbol)) {
                return symbol.Rva != 0;
            }
            return false;
        }

        private static void ParseExports(Func<ulong, byte[], int, bool> read, ulong moduleBase, CacheEntry entry) {
            byte[] hdr = new byte[HEADER_READ];
            if (!read(moduleBase, hdr, hdr.Length)) return;
            if (!(hdr[0] == 'M' && hdr[1] == 'Z')) return;
            int e_lfanew = BitConverter.ToInt32(hdr, 0x3C);
            if (e_lfanew <= 0 || e_lfanew + 0x90 > hdr.Length) return;
            if (!(hdr[e_lfanew] == 'P' && hdr[e_lfanew + 1] == 'E')) return;
            int optOffset = e_lfanew + 24;
            bool isPE32Plus = BitConverter.ToUInt16(hdr, optOffset) == 0x20b;
            int dataDirCountOffset = isPE32Plus ? optOffset + 108 : optOffset + 92;
            ushort numberOfRva = BitConverter.ToUInt16(hdr, dataDirCountOffset);
            int dataDirBase = isPE32Plus ? optOffset + 112 : optOffset + 96;
            if (numberOfRva <= IMAGE_DIRECTORY_ENTRY_EXPORT) return;
            int exportDirEntry = dataDirBase + (int)IMAGE_DIRECTORY_ENTRY_EXPORT * 8;
            uint exportRva = BitConverter.ToUInt32(hdr, exportDirEntry);
            uint exportSize = BitConverter.ToUInt32(hdr, exportDirEntry + 4);
            if (exportRva == 0 || exportSize == 0) return;

            byte[] exportDir = new byte[40];
            if (!read(moduleBase + exportRva, exportDir, exportDir.Length)) return;
            uint numberOfFunctions = BitConverter.ToUInt32(exportDir, 20); // NumberOfFunctions
            uint numberOfNames = BitConverter.ToUInt32(exportDir, 24);
            uint addressOfFunctions = BitConverter.ToUInt32(exportDir, 28);
            uint addressOfNames = BitConverter.ToUInt32(exportDir, 32);
            uint addressOfNameOrdinals = BitConverter.ToUInt32(exportDir, 36);
            if (numberOfNames == 0 || numberOfFunctions == 0) return;

            byte[] nameRvas = new byte[numberOfNames * 4];
            if (!read(moduleBase + addressOfNames, nameRvas, nameRvas.Length)) return;
            byte[] ordinals = new byte[numberOfNames * 2];
            if (!read(moduleBase + addressOfNameOrdinals, ordinals, ordinals.Length)) return;

            // We only fetch function RVAs on demand while iterating names, to avoid large alloc for rare huge tables.
            for (uint i = 0; i < numberOfNames; i++) {
                uint nameRva = BitConverter.ToUInt32(nameRvas, (int)(i * 4));
                string name = ReadAsciiZ(read, moduleBase + nameRva, 256);
                if (string.IsNullOrEmpty(name)) continue;
                ushort ordinalIndex = BitConverter.ToUInt16(ordinals, (int)(i * 2));
                if (ordinalIndex >= numberOfFunctions) continue;
                byte[] funcRvaBuf = new byte[4];
                if (!read(moduleBase + addressOfFunctions + (ulong)ordinalIndex * 4UL, funcRvaBuf, 4)) continue;
                uint symbolRva = BitConverter.ToUInt32(funcRvaBuf, 0);
                var sym = new ExportSymbol { Rva = symbolRva, LooksLikeFunction = ClassifyFunctionHeuristic(read, moduleBase + symbolRva) };
                entry.Symbols[name] = sym;
            }
        }

        private static bool ClassifyFunctionHeuristic(Func<ulong, byte[], int, bool> read, ulong address) {
            byte[] b = new byte[1];
            if (!read(address, b, 1)) return false;
            switch (b[0]) {
                case 0x48: // REX prefix (likely function)
                case 0x55: // push rbp
                case 0x40: // REX
                case 0x4C: // REX
                case 0x53: // push rbx
                case 0x56: // push rsi
                case 0x57: // push rdi
                case 0x41: // push r? (extended)
                    return true;
                default:
                    return false;
            }
        }

        private static string ReadAsciiZ(Func<ulong, byte[], int, bool> read, ulong address, int maxLen) {
            byte[] tmp = new byte[maxLen];
            if (!read(address, tmp, maxLen)) return string.Empty;
            int len = 0; while (len < maxLen && tmp[len] != 0) len++;
            return Encoding.ASCII.GetString(tmp, 0, len);
        }
    }
}
