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
#include <mediaobj.h>

class AudioStream;

class MediaBuffer : public IMediaBuffer {
    static const unsigned int _max_buffer_length = 4096;

    ULONG _refCount;

public:
    AudioStream* _parentStream;
    BYTE _buffer[_max_buffer_length];
    DWORD _length;

    MediaBuffer (AudioStream* parentStream);

    void ReInit();

    virtual HRESULT STDMETHODCALLTYPE SetLength(DWORD cbLength);
    virtual HRESULT STDMETHODCALLTYPE GetMaxLength(__out  DWORD *pcbMaxLength);
    virtual HRESULT STDMETHODCALLTYPE GetBufferAndLength(__deref_opt_out_bcount(*pcbLength)  BYTE **ppBuffer, __out_opt  DWORD *pcbLength);
    virtual HRESULT STDMETHODCALLTYPE QueryInterface(__in REFIID riid, __RPC__deref_out void __RPC_FAR *__RPC_FAR *ppvObject);
    virtual ULONG STDMETHODCALLTYPE AddRef( void);
    virtual ULONG STDMETHODCALLTYPE Release( void);
};