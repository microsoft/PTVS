// Visual Studio Shared Project
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

namespace TestUtilities.Mocks
{
    public class MockTextDocument : ITextDocument
    {
        private string _filePath;
        private readonly ITextBuffer _buffer;

        public MockTextDocument(ITextBuffer buffer, string filePath)
        {
            _buffer = buffer;
            _filePath = filePath;
        }


        #region ITextDocument Members

        public event EventHandler DirtyStateChanged
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public Encoding Encoding
        {
            get
            {
                return Encoding.UTF8;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public event EventHandler<EncodingChangedEventArgs> EncodingChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<TextDocumentFileActionEventArgs> FileActionOccurred
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public string FilePath
        {
            get { return _filePath; }
        }

        public bool IsDirty
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsReloading
        {
            get { throw new NotImplementedException(); }
        }

        public DateTime LastContentModifiedTime
        {
            get { throw new NotImplementedException(); }
        }

        public DateTime LastSavedTime
        {
            get { throw new NotImplementedException(); }
        }

        public ReloadResult Reload(EditOptions options)
        {
            throw new NotImplementedException();
        }

        public ReloadResult Reload()
        {
            throw new NotImplementedException();
        }

        public void Rename(string newFilePath)
        {
            _filePath = newFilePath;
        }

        public void Save()
        {
            File.WriteAllText(_filePath, TextBuffer.CurrentSnapshot.GetText());
        }

        public void SaveAs(string filePath, bool overwrite, bool createFolder, Microsoft.VisualStudio.Utilities.IContentType newContentType)
        {
            throw new NotImplementedException();
        }

        public void SaveAs(string filePath, bool overwrite, Microsoft.VisualStudio.Utilities.IContentType newContentType)
        {
            throw new NotImplementedException();
        }

        public void SaveAs(string filePath, bool overwrite, bool createFolder)
        {
            throw new NotImplementedException();
        }

        public void SaveAs(string filePath, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public void SaveCopy(string filePath, bool overwrite, bool createFolder)
        {
            throw new NotImplementedException();
        }

        public void SaveCopy(string filePath, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public void SetEncoderFallback(EncoderFallback fallback)
        {
            throw new NotImplementedException();
        }

        public ITextBuffer TextBuffer
        {
            get { return _buffer; }
        }

        public void UpdateDirtyState(bool isDirty, DateTime lastContentModifiedTime)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
        }

        #endregion
    }
}
