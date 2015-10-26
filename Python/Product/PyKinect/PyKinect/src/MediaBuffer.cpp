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

// Implements IMediaBuffer - this just stores enough data for reading
MediaBuffer::MediaBuffer (AudioStream* parentStream) {
    _parentStream = parentStream;
    ReInit();
}

void MediaBuffer::ReInit() {
    _refCount = 1;
    _length = 0;
}

HRESULT STDMETHODCALLTYPE MediaBuffer::SetLength(
    DWORD cbLength) {
    _length = cbLength;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE MediaBuffer::GetMaxLength(
    /* [annotation][out] */
    __out  DWORD *pcbMaxLength) {
    *pcbMaxLength = _max_buffer_length;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE MediaBuffer::GetBufferAndLength(
    /* [annotation][out] */
    __deref_opt_out_bcount(*pcbLength)  BYTE **ppBuffer,
    /* [annotation][out] */
    __out_opt  DWORD *pcbLength) {
    if(ppBuffer == nullptr || pcbLength == nullptr) {
        return E_POINTER;
    }

    *ppBuffer = _buffer;
    *pcbLength = _length;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE MediaBuffer::QueryInterface(
    /* [in] */ REFIID riid,
    /* [iid_is][out] */ __RPC__deref_out void __RPC_FAR *__RPC_FAR *ppvObject) {
    if(riid == __uuidof(IUnknown)) {
        AddRef();
        *ppvObject = static_cast<IUnknown*>(this);
        return S_OK;
    }else if(riid == __uuidof(IMediaBuffer)) {
        AddRef();
        *ppvObject = static_cast<IMediaBuffer*>(this);
        return S_OK;
    }else{
        *ppvObject = nullptr;
        return E_NOINTERFACE;
    }
};

ULONG STDMETHODCALLTYPE MediaBuffer::AddRef( void) {
    return InterlockedIncrement(&_refCount);
}

ULONG STDMETHODCALLTYPE MediaBuffer::Release( void) {
    long refCount = InterlockedDecrement(&_refCount);
    if(refCount == 0) {
        auto parent = _parentStream;
        if(parent == nullptr) {
            delete this;
        }else{
            parent->FreeBuffer(this);
        }
        return 0;
    }
    return refCount;
}
