/* PyKinect
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.
*/

#include "stdafx.h"
#include "PyKinectAudio.h"

/***************************************************************************************
* ISpStreamFormat implementation
*
*/

int TargetDurationInSec = 10;

AudioStream::AudioStream(IMediaObject* mediaObject, DWORD readStaleThreshold) {
    _refCount = 1;
    _mediaObject = mediaObject;
    InitializeCriticalSection(&_lock);

    // our wave format is fixed, so these all end up being constants...
    _CurrentCaptureLength = 0;
    _curBuffer = nullptr;
    _CurrentReadIndex = 0;
    _count = 0;
    _shouldExit = false;
    _readStaleThreshold = readStaleThreshold;
    _readCallback = nullptr;

    DWORD threadId;
    _captureThread = CreateThread(NULL, 0, CaptureThread, this, 0, &threadId);
    if(_captureThread == nullptr) {
        throw std::exception("Failed to create capture thread");
    }

    // our thread needs to keep us alive until we request that we exit...
    AddRef();

    mediaObject->AddRef();
}

AudioStream::AudioStream(ReadCallback callback) {
    InitializeCriticalSection(&_lock);

    _readCallback = callback;
    _curBuffer = nullptr;
    _refCount = 1;
    _mediaObject = nullptr;
    _captureThread = nullptr;
}

AudioStream::~AudioStream() {
    // lock needs to be released before we delete this
    LockHolder lock(this);

    // clean up all of the saved media buffers

    // buffers in flight should free themselves when they're released, we indicate this via
    // clearing their parent which they'll check (and we'll check again if an external consumer
    // of them Releases them while this is in flight)
    for(auto front = this->_buffers.begin(); front != this->_buffers.end(); front++) {
        (*front)->_parentStream = nullptr;
    }

    // then delete the cached free buffers
    while(!this->_freeBuffers.empty()) {
        delete this->_freeBuffers.front();
        this->_freeBuffers.pop();
    }

    if(_mediaObject != nullptr) {

        _mediaObject->Release();
    }
}

void AudioStream::FreeBuffer(MediaBuffer* buffer) {
    LockHolder lock(this);

    // need to check parent stream here again because we now finally hold the lock
    if(buffer->_parentStream == nullptr) {
        delete buffer;
    }else{
        _freeBuffers.push(buffer);
    }
}

MediaBuffer* AudioStream::GetBuffer() {
    LockHolder lock(this);

    if(_freeBuffers.empty()) {
        return new (nothrow) MediaBuffer(this);
    }else{
        auto res = _freeBuffers.front();
        _freeBuffers.pop();
        res->ReInit();
        return res;
    }
}

DWORD WINAPI AudioStream::CaptureThread(LPVOID thisObj) {
    CoInitializeEx(NULL, COINIT_MULTITHREADED);

    auto self = (AudioStream*)thisObj;
    while(!self->_shouldExit) {
        DMO_OUTPUT_DATA_BUFFER outputBuffer;
        DWORD status;
        auto buffer = self->GetBuffer();
        if(buffer == nullptr) {
            continue;
        }

        memset(&outputBuffer, 0, sizeof(DMO_OUTPUT_DATA_BUFFER ));

        outputBuffer.pBuffer = buffer;    

        HRESULT hr = self->_mediaObject->ProcessOutput(0, 1, &outputBuffer, &status);
        if(SUCCEEDED(hr)) {
            LockHolder lock(self);

            self->_buffers.push_back(buffer);
        }
    }

    // we hold a ref to keep us alive, close us now.
    CloseHandle(self->_captureThread);
    self->Release();
    return 0;
}

HRESULT STDMETHODCALLTYPE AudioStream::SetNotifySink( 
    /* [in] */ ISpNotifySink *pNotifySink)  {
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::SetNotifyWindowMessage( 
    /* [in] */ HWND hWnd,
    /* [in] */ UINT Msg,
    /* [in] */ WPARAM wParam,
    /* [in] */ LPARAM lParam) {
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::SetNotifyCallbackFunction( 
    /* [in] */ SPNOTIFYCALLBACK *pfnCallback,
    /* [in] */ WPARAM wParam,
    /* [in] */ LPARAM lParam) {
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::SetNotifyCallbackInterface( 
    /* [in] */ ISpNotifyCallback *pSpCallback,
    /* [in] */ WPARAM wParam,
    /* [in] */ LPARAM lParam) {
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::SetNotifyWin32Event( void) {
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::WaitForNotifyEvent( 
    /* [in] */ DWORD dwMilliseconds) {
        return S_OK;
}

HANDLE STDMETHODCALLTYPE AudioStream::GetNotifyEventHandle( void) {
    return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::AddEvents( 
    /* [in] */ const SPEVENT *pEventArray,
    /* [in] */ ULONG ulCount) { 
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::GetEventInterest( 
    /* [out] */ ULONGLONG *pullEventInterest) {
        pullEventInterest = 0;
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::SetInterest( 
    /* [in] */ ULONGLONG ullEventInterest,
    /* [in] */ ULONGLONG ullQueuedInterest) { 
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::GetEvents( 
    /* [in] */ ULONG ulCount,
    /* [size_is][out] */ SPEVENT *pEventArray,
    /* [out] */ ULONG *pulFetched){ 
        *pulFetched = 0;
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::GetInfo( 
    /* [out] */ SPEVENTSOURCEINFO *pInfo) { 
        return E_FAIL;
}

HRESULT STDMETHODCALLTYPE AudioStream::GetFormat( 
    GUID *pguidFormatId,
    WAVEFORMATEX **ppCoMemWaveFormatEx) {            
        *ppCoMemWaveFormatEx = (WAVEFORMATEX*)CoTaskMemAlloc(sizeof(WAVEFORMATEX));
        if(*ppCoMemWaveFormatEx == nullptr) {
            return E_OUTOFMEMORY;
        }

        auto format = WAVEFORMATEX();
        format.cbSize = 0;
        format.nChannels = 1;
        format.nSamplesPerSec = 16000;
        format.nAvgBytesPerSec = 32000;
        format.nBlockAlign = 2;
        format.wBitsPerSample = 16;
        format.wFormatTag = WAVE_FORMAT_PCM;

        memcpy(*ppCoMemWaveFormatEx, &format, sizeof(WAVEFORMATEX));

        static const GUID waveFormatGuid = 
        { 0xC31ADBAE, 0x527F, 0x4ff5, { 0xA2, 0x30, 0xF6, 0x2B, 0xB6, 0x1F, 0xF7, 0x0C } };

        *pguidFormatId = waveFormatGuid;
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::QueryInterface( 
    /* [in] */ REFIID riid,
    /* [iid_is][out] */ __RPC__deref_out void __RPC_FAR *__RPC_FAR *ppvObject) {
        if(riid == __uuidof(IUnknown)) {
            AddRef();
            *ppvObject = static_cast<IUnknown*>(static_cast<ISpStreamFormat*>(this));
            return S_OK;
        }else if(riid == __uuidof(ISpStreamFormat)) {
            AddRef();
            *ppvObject = static_cast<ISpStreamFormat*>(this);
            return S_OK;
        }else if(riid == __uuidof(ISpEventSink)) {
            AddRef();
            *ppvObject = static_cast<ISpEventSink*>(this);
            return S_OK;
        }else if(riid == __uuidof(ISpEventSource)) {
            AddRef();
            *ppvObject = static_cast<ISpEventSource*>(this);
            return S_OK;
        }else{
            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }
};

ULONG STDMETHODCALLTYPE AudioStream::AddRef( void) { 
    return InterlockedIncrement(&_refCount);
}

ULONG STDMETHODCALLTYPE AudioStream::Release( void) {
    long refCount = InterlockedDecrement(&_refCount);
    if(refCount == 0) {
        delete this;

        return 0;
    }else if(refCount == 1 && _captureThread != nullptr) {
        // our capture thread holds the last ref count and should
        // now exit.
        _shouldExit = true;
    }
    return refCount;
}

HRESULT STDMETHODCALLTYPE AudioStream::Read(__out_bcount_part(cb, *pcbRead)  void *pv, ULONG cb,__out_opt  ULONG *pcbRead) {
    if(_readCallback != nullptr) {
        // reading from a Python file like object...
        return _readCallback(cb, pv, pcbRead);
    }

    // reading from our own MediaBuffer queue
    *pcbRead = 0;
    ULONG bytesRead = 0;
    while(bytesRead != cb) {		
        if(_CurrentReadIndex != _CurrentCaptureLength) {
            // copy any bytes we have
            auto toRead = min(cb - bytesRead, _CurrentCaptureLength - _CurrentReadIndex);
            memcpy((BYTE*)pv + bytesRead, &_curBuffer->_buffer[_CurrentReadIndex], toRead);
            _CurrentReadIndex += toRead;

            bytesRead += toRead;
        }

        if(bytesRead != cb) {
            // read more bytes
            LockHolder lock(this);
            if(_buffers.begin() != _buffers.end()) {
                if(_curBuffer != nullptr) {
                    _curBuffer->Release();
                }

                _curBuffer = _buffers.front();
                _buffers.pop_front();

                _CurrentCaptureLength = _curBuffer->_length;
                _CurrentReadIndex = 0;
            }
        }
    }

    if(pcbRead != nullptr) {
        // optional
        *pcbRead = bytesRead;
    }
    return S_OK;
}

/* [local] */ HRESULT STDMETHODCALLTYPE AudioStream::Write( 
    /* [annotation] */ 
    __in_bcount(cb)  const void *pv,
    /* [in] */ ULONG cb,
    /* [annotation] */ 
    __out_opt  ULONG *pcbWritten)  {
        return E_NOTIMPL;
}

/* [local] */ HRESULT STDMETHODCALLTYPE AudioStream::Seek( 
    /* [in] */ LARGE_INTEGER dlibMove,
    /* [in] */ DWORD dwOrigin,
    /* [annotation] */ 
    __out_opt  ULARGE_INTEGER *plibNewPosition)  {
        if(plibNewPosition != nullptr) {
            plibNewPosition->QuadPart = 0;
        }
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::SetSize( 
    /* [in] */ ULARGE_INTEGER libNewSize) {
        return E_NOTIMPL;
}

/* [local] */ HRESULT STDMETHODCALLTYPE AudioStream::CopyTo( 
    /* [unique][in] */ IStream *pstm,
    /* [in] */ ULARGE_INTEGER cb,
    /* [annotation] */ 
    __out_opt  ULARGE_INTEGER *pcbRead,
    /* [annotation] */ 
    __out_opt  ULARGE_INTEGER *pcbWritten)  {
        return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE AudioStream::Commit( 
    /* [in] */ DWORD grfCommitFlags)  {
        return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE AudioStream::Revert( void)  {
    return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE AudioStream::LockRegion( 
    /* [in] */ ULARGE_INTEGER libOffset,
    /* [in] */ ULARGE_INTEGER cb,
    /* [in] */ DWORD dwLockType)  {
        return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE AudioStream::UnlockRegion( 
    /* [in] */ ULARGE_INTEGER libOffset,
    /* [in] */ ULARGE_INTEGER cb,
    /* [in] */ DWORD dwLockType)  {
        return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE AudioStream::Stat( 
    /* [out] */ __RPC__out STATSTG *pstatstg,
    /* [in] */ DWORD grfStatFlag)  {
        pstatstg->cbSize.QuadPart = INFINITE;
        return S_OK;
}

HRESULT STDMETHODCALLTYPE AudioStream::Clone( 
    /* [out] */ __RPC__deref_out_opt IStream **ppstm)  {
        return E_NOTIMPL;
}
