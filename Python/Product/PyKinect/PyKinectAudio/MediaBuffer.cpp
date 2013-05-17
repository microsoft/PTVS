/* 
 * ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 * for more information.
 *
 * ***************************************************************************/



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
