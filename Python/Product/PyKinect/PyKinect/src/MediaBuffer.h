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