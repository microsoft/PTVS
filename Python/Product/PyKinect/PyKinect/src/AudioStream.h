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

#pragma once

#include "stdafx.h"
#include <Windows.h>
#include <sapi.h>
#include <wmcodecdsp.h>	// For configuring DMO properties (MFPKEY_WMAAECMA_SYSTEM_MODE, MFPKEY_WMAAECMA_DMO_SOURCE_MODE)
#include <mfapi.h>      // MF_MT_* constants
#include <deque>
#include <queue>

using namespace std;

typedef HRESULT(__stdcall ReadCallback)(DWORD bytes, void* text, ULONG* pcbRead);

class AudioStream : public ISpStreamFormat, public ISpEventSink, public ISpEventSource {
public:
    IMediaObject* _mediaObject;

private:
    ULONG _refCount;
    size_t _CurrentCaptureLength, _CurrentReadIndex;
    MediaBuffer *_curBuffer;
    int _count;
    CRITICAL_SECTION _lock;
    deque<MediaBuffer*> _buffers;
    queue<MediaBuffer*> _freeBuffers;
    bool _shouldExit;
    DWORD _readStaleThreshold;
    ReadCallback* _readCallback;
    HANDLE _captureThread;

    class LockHolder {
        AudioStream* _stream;
    public:
        LockHolder(AudioStream* stream) {
            _stream = stream;
            EnterCriticalSection(&_stream->_lock);
        }

        ~LockHolder() {
            LeaveCriticalSection(&_stream->_lock);
        }
    };

public:
    AudioStream(IMediaObject* mediaObject, DWORD readStaleThreshold);
    AudioStream(ReadCallback readCallback);
    ~AudioStream();

    // Frees a buffer, saving it in our queue of cached buffers if we're still running.
    void FreeBuffer(MediaBuffer* buffer);

    // Gets a new buffer, pulling it from the cache if available, or creating a new buffer if not.
    MediaBuffer* GetBuffer();

    void Stop() {
        _shouldExit = true;
    }

    static DWORD WINAPI CaptureThread(LPVOID thisObj);

    virtual HRESULT STDMETHODCALLTYPE SetNotifySink(/* [in] */ ISpNotifySink *pNotifySink);

    virtual /* [local] */ HRESULT STDMETHODCALLTYPE SetNotifyWindowMessage(
        /* [in] */ HWND hWnd,
        /* [in] */ UINT Msg,
        /* [in] */ WPARAM wParam,
        /* [in] */ LPARAM lParam);

    virtual /* [local] */ HRESULT STDMETHODCALLTYPE SetNotifyCallbackFunction(
        /* [in] */ SPNOTIFYCALLBACK *pfnCallback,
        /* [in] */ WPARAM wParam,
        /* [in] */ LPARAM lParam);

    virtual /* [local] */ HRESULT STDMETHODCALLTYPE SetNotifyCallbackInterface(
        /* [in] */ ISpNotifyCallback *pSpCallback,
        /* [in] */ WPARAM wParam,
        /* [in] */ LPARAM lParam);


    virtual /* [local] */ HRESULT STDMETHODCALLTYPE SetNotifyWin32Event(void);

    virtual /* [local] */ HRESULT STDMETHODCALLTYPE WaitForNotifyEvent(
        /* [in] */ DWORD dwMilliseconds);

    virtual /* [local] */ HANDLE STDMETHODCALLTYPE GetNotifyEventHandle(void);

    virtual HRESULT STDMETHODCALLTYPE AddEvents(
        /* [in] */ const SPEVENT *pEventArray,
        /* [in] */ ULONG ulCount);

    virtual HRESULT STDMETHODCALLTYPE GetEventInterest(
        /* [out] */ ULONGLONG *pullEventInterest);

    virtual HRESULT STDMETHODCALLTYPE SetInterest(
        /* [in] */ ULONGLONG ullEventInterest,
        /* [in] */ ULONGLONG ullQueuedInterest);

    virtual HRESULT STDMETHODCALLTYPE GetEvents(
        /* [in] */ ULONG ulCount,
        /* [size_is][out] */ SPEVENT *pEventArray,
        /* [out] */ ULONG *pulFetched);

    virtual HRESULT STDMETHODCALLTYPE GetInfo(
        /* [out] */ SPEVENTSOURCEINFO *pInfo);

    virtual HRESULT STDMETHODCALLTYPE GetFormat(GUID *pguidFormatId, WAVEFORMATEX **ppCoMemWaveFormatEx);

    virtual HRESULT STDMETHODCALLTYPE QueryInterface(__in REFIID riid, __RPC__deref_out void __RPC_FAR *__RPC_FAR *ppvObject);

    virtual ULONG STDMETHODCALLTYPE AddRef(void);

    virtual ULONG STDMETHODCALLTYPE Release(void);

    virtual /* [local] */ HRESULT STDMETHODCALLTYPE Read(
        /* [annotation] */
        __out_bcount_part(cb, *pcbRead)  void *pv,
        /* [in] */ ULONG cb,
        /* [annotation] */
        __out_opt  ULONG *pcbRead);

    virtual /* [local] */ HRESULT STDMETHODCALLTYPE Write(
        /* [annotation] */
        __in_bcount(cb)  const void *pv,
        /* [in] */ ULONG cb,
        /* [annotation] */
        __out_opt  ULONG *pcbWritten);

    virtual /* [local] */ HRESULT STDMETHODCALLTYPE Seek(
        /* [in] */ LARGE_INTEGER dlibMove,
        /* [in] */ DWORD dwOrigin,
        /* [annotation] */
        __out_opt  ULARGE_INTEGER *plibNewPosition);

    virtual HRESULT STDMETHODCALLTYPE SetSize(
        /* [in] */ ULARGE_INTEGER libNewSize);

    virtual /* [local] */ HRESULT STDMETHODCALLTYPE CopyTo(
        /* [unique][in] */ IStream *pstm,
        /* [in] */ ULARGE_INTEGER cb,
        /* [annotation] */
        __out_opt  ULARGE_INTEGER *pcbRead,
        /* [annotation] */
        __out_opt  ULARGE_INTEGER *pcbWritten);

    virtual HRESULT STDMETHODCALLTYPE Commit(
        /* [in] */ DWORD grfCommitFlags);

    virtual HRESULT STDMETHODCALLTYPE Revert(void);

    virtual HRESULT STDMETHODCALLTYPE LockRegion(
        /* [in] */ ULARGE_INTEGER libOffset,
        /* [in] */ ULARGE_INTEGER cb,
        /* [in] */ DWORD dwLockType);

    virtual HRESULT STDMETHODCALLTYPE UnlockRegion(
        /* [in] */ ULARGE_INTEGER libOffset,
        /* [in] */ ULARGE_INTEGER cb,
        /* [in] */ DWORD dwLockType);

    virtual HRESULT STDMETHODCALLTYPE Stat(
        /* [out] */ __RPC__out STATSTG *pstatstg,
        /* [in] */ DWORD grfStatFlag);

    virtual HRESULT STDMETHODCALLTYPE Clone(
        /* [out] */ __RPC__deref_out_opt IStream **ppstm);
};