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
#include <NuiApi.h>
#include <uuids.h>
#include <sphelper.h>

using namespace std;

typedef void (__stdcall _RecognizeCallback)(LPWSTR text);
typedef void (__stdcall _EnumRecognizersCallback)(LPWSTR id, LPWSTR description, ISpObjectToken* token);
#pragma comment(lib, "strmiids.lib")

// Flat C API for exposing to Python
extern "C" {
    __declspec(dllexport) HRESULT OpenKinectAudio(INuiSensor* pSensor, IMediaObject** ppDMO) {
        IMediaObject* pDMO;
        INuiAudioBeam* pAudioBeam;
        HRESULT hr = pSensor->NuiGetAudioSource(&pAudioBeam);
        if(FAILED(hr)) {
            return hr;
        }

        hr = pAudioBeam->QueryInterface(IID_IMediaObject, (void**)&pDMO);
        if(FAILED(hr)) {
            return hr;
        }

        IPropertyStore* pPS = NULL;
        hr = pDMO->QueryInterface(IID_IPropertyStore, (void**)&pPS);
        if(FAILED(hr)) {
            pDMO->Release();
            return hr;
        }

        // Set MicArray DMO system mode with no echo cancellation.
        // This must be set for the DMO to work properly
        PROPVARIANT pvSysMode;
        PropVariantInit(&pvSysMode);
        pvSysMode.vt = VT_I4;

        pvSysMode.lVal = (LONG)(OPTIBEAM_ARRAY_ONLY);
        hr = pPS->SetValue(MFPKEY_WMAAECMA_SYSTEM_MODE, pvSysMode);
        PropVariantClear(&pvSysMode);

        // Put media object into filter mode so it can be used as a Media Foundation transform
        PROPVARIANT pvSourceMode;
        PropVariantInit(&pvSourceMode);    
        pvSourceMode.vt = VT_BOOL;
        pvSourceMode.boolVal = VARIANT_TRUE;
        hr = pPS->SetValue(MFPKEY_WMAAECMA_DMO_SOURCE_MODE, pvSourceMode);
        pPS->Release();

        if(FAILED(hr)) {
            pDMO->Release();
            return hr;
        }

        DMO_MEDIA_TYPE type;
        memset(&type, 0, sizeof(DMO_MEDIA_TYPE));
        type.majortype = MFMediaType_Audio;
        type.subtype = MFAudioFormat_PCM;
        type.lSampleSize = 0;
        type.bFixedSizeSamples = true;
        type.bTemporalCompression = false;
        type.formattype = FORMAT_WaveFormatEx;
        type.cbFormat = sizeof(WAVEFORMATEX);
        type.pbFormat = (BYTE*)CoTaskMemAlloc(sizeof(WAVEFORMATEX));
        if(type.pbFormat == nullptr) {
            pDMO->Release();
            return E_OUTOFMEMORY;    
        }

        WAVEFORMATEX *waveformatex = (WAVEFORMATEX*)type.pbFormat;
        waveformatex->wFormatTag = WAVE_FORMAT_PCM;
        waveformatex->nChannels = 1;
        waveformatex->nSamplesPerSec = 0x3e80;
        waveformatex->nAvgBytesPerSec = 0x7d00;
        waveformatex->nBlockAlign = 2;
        waveformatex->wBitsPerSample = 0x10;
        waveformatex->cbSize = 0x0;

        hr = pDMO->SetOutputType(0, &type, 0);
        if(FAILED(hr)) {
            pDMO->Release();
            return hr;
        }

        hr = pDMO->AllocateStreamingResources();
        if(FAILED(hr)) {
            pDMO->Release();
            return hr;
        }

        *ppDMO = pDMO;
        return S_OK;
    }

    __declspec(dllexport) HRESULT OpenAudioStream(IMediaObject* pDMO, ISpStreamFormat** stream, DWORD readStaleThreshold) {
        *stream = new AudioStream(pDMO, readStaleThreshold);		
        return S_OK;
    }

    __declspec(dllexport) HRESULT ReadAudioStream(ISpStreamFormat* stream, void* data, ULONG cb, ULONG* pcbRead) {
        return stream->Read(data, cb, pcbRead);
    }

    __declspec(dllexport) void IUnknownRelease(IUnknown* obj) {
        obj->Release();
    }

    __declspec(dllexport) HRESULT EnumRecognizers(_EnumRecognizersCallback callback) {
        IEnumSpObjectTokens *enumTokens;
        HRESULT hr = SpEnumTokens(SPCAT_RECOGNIZERS, NULL, NULL, &enumTokens);
        if(FAILED(hr)) {
            return hr;
        }

        ISpObjectToken *token;
        ULONG fetched;
        while(SUCCEEDED(enumTokens->Next(1, &token, &fetched)) && fetched == 1) {
            LPWSTR id = nullptr;
            hr = token->GetId(&id);

            if(SUCCEEDED(hr)) {
                LPWSTR description = nullptr;
                hr = token->GetStringValue(L"", &description);				

                if(SUCCEEDED(hr)) {
                    callback(id, description, token);				
                    ::CoTaskMemFree(description);
                }else{
                    token->Release();
                }

                ::CoTaskMemFree(id);

                // token is now owned in Python
            } else {
                token->Release();
            }

        }
        enumTokens->Release();
        return S_OK;
    }

    __declspec(dllexport) HRESULT CreateRecognizer(ISpObjectToken* token, ISpRecoContext** ppContext) {
        ISpRecognizer * reco;
        HRESULT hr = CoCreateInstance(CLSID_SpInprocRecognizer, NULL, CLSCTX_INPROC_SERVER, IID_ISpRecognizer, (LPVOID*)&reco);
        if(FAILED(hr)) {
            return hr;
        }

        if(token != nullptr) {
            hr = reco->SetRecognizer(token);
            if(FAILED(hr)) {
                return hr;
            }
        }

        ISpRecoContext *context;
        hr = reco->CreateRecoContext(&context);
        if(FAILED(hr)) {
            reco->Release();
            return hr;
        }

        reco->Release();
        *ppContext = context;
        return S_OK;
    }

    __declspec(dllexport) HRESULT LoadGrammar(LPCWSTR filename, ISpRecoContext* context, ISpRecoGrammar** ppGrammar) {
        ISpRecoGrammar* grammar;    
        HRESULT hr = context->CreateGrammar(1, &grammar);
        if(FAILED(hr)) {
            return hr;
        }
        ISpRecognizer* reco;
        hr = context->GetRecognizer(&reco);
        if(FAILED(hr)) {
            return hr;
        }

        hr = grammar->LoadCmdFromFile(filename, SPLO_STATIC);
        if(FAILED(hr)) {
            context->Release();
            reco->Release();
            return hr;
        }

        hr = grammar->SetRuleState(NULL, NULL, SPRS_ACTIVE);
        if(FAILED(hr)) {
            context->Release();
            reco->Release();
            return hr;
        }

        reco->Release();
        *ppGrammar = grammar;
        return S_OK;
    }

    __declspec(dllexport) HRESULT RecognizeOne(ISpRecoContext* pContext, DWORD timeout, _RecognizeCallback callback, _RecognizeCallback altCallback) {
        HRESULT hr = pContext->WaitForNotifyEvent(timeout);
        if(FAILED(hr)) {
            return hr;
        }

        SPEVENT curEvent;
        ULONG fetched;
        hr = pContext->GetEvents(1, &curEvent, &fetched);
        if(FAILED(hr)) {
            return hr;
        }

        if(curEvent.eEventId == SPEI_RECOGNITION) {

            ISpRecoResult* result = reinterpret_cast<ISpRecoResult*>(curEvent.lParam);
            const USHORT               MAX_ALTERNATES = 100;
            ISpPhraseAlt*      pcpPhraseAlt[MAX_ALTERNATES];
            ULONG altCount;

            SPPHRASE* phrase;
            hr = result->GetPhrase(&phrase);
            if(FAILED(hr)) {
                return hr;
            }

            WCHAR *pwszText = nullptr;
            hr = result->GetText(SP_GETWHOLEPHRASE, SP_GETWHOLEPHRASE, TRUE, &pwszText, NULL);
            if(!FAILED(hr)) {							
                callback(pwszText);
                ::CoTaskMemFree(pwszText);
            }

            hr = result->GetAlternates(phrase->Rule.ulFirstElement,
                phrase->Rule.ulCountOfElements,
                MAX_ALTERNATES,
                pcpPhraseAlt,
                &altCount);


            if(SUCCEEDED(hr)) {
                for(ULONG i = 0; i<altCount; i++) {
                    hr = pcpPhraseAlt[i]->GetText(SP_GETWHOLEPHRASE, SP_GETWHOLEPHRASE, TRUE, &pwszText, NULL);
                    if (!FAILED(hr)) {
                        altCallback(pwszText);
                        // TODO: Could hold onto the phrase and send it back to Python so it can be committed.
                        ::CoTaskMemFree(pwszText);
                    }
                }

                ::CoTaskMemFree(phrase);
            }

            altCallback(nullptr);

            return S_OK;
        }

        return S_FALSE;
    }

    class CallbackInfo {
    public:
        _RecognizeCallback *Callback, *AltCallback;
        ISpRecoContext* pContext;
        HANDLE cancelHandle;
        HANDLE waitHandle;
        bool multiple;
    };

    DWORD WINAPI AsyncRecognizeThread(LPVOID param) {
        CallbackInfo* cbInfo = (CallbackInfo*)param;
        HANDLE handles[2] = {cbInfo->cancelHandle, cbInfo->waitHandle};

        do {
            auto waitIndex = ::WaitForMultipleObjects(2, handles, FALSE, INFINITE);
            if(waitIndex == WAIT_OBJECT_0) {
                return 0;
            }else if(waitIndex == WAIT_OBJECT_0 + 1) {
                RecognizeOne(cbInfo->pContext, 0, cbInfo->Callback, cbInfo->AltCallback);			
            }
        }while(cbInfo->multiple);

        return 0;
    }

    __declspec(dllexport) HRESULT RecognizeAsync(ISpRecoContext* pContext, bool multiple, _RecognizeCallback callback, _RecognizeCallback altCallback, HANDLE* pCancelHandle) {
        HANDLE waitHandle = pContext->GetNotifyEventHandle();
        if(waitHandle == INVALID_HANDLE_VALUE) {
            // "interface is not initialized" according to http://msdn.microsoft.com/en-us/library/ee450842(v=vs.85).aspx
            return E_FAIL;
        }

        HANDLE cancelHandle = ::CreateEventA(NULL, TRUE, FALSE, NULL);
        if(cancelHandle == nullptr) {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        CallbackInfo* cbInfo = new (nothrow) CallbackInfo;
        if(cbInfo == nullptr) {
            ::CloseHandle(cancelHandle);
            return E_OUTOFMEMORY;
        }

        cbInfo->Callback = callback;
        cbInfo->AltCallback = altCallback;
        cbInfo->cancelHandle = cancelHandle;
        cbInfo->pContext = pContext;
        cbInfo->waitHandle = waitHandle;
        cbInfo->multiple = multiple;

        DWORD threadId;
        HANDLE threadHandle = ::CreateThread(NULL, 0, AsyncRecognizeThread, cbInfo, 0, &threadId);
        if(threadHandle == nullptr) {
            delete cbInfo;
            ::CloseHandle(cancelHandle);
            return HRESULT_FROM_WIN32(GetLastError());
        }
        CloseHandle(threadHandle);


        return S_OK;
    }

    __declspec(dllexport) HRESULT StopRecognizeAsync(HANDLE cancelHandle) {
        if(!::SetEvent(cancelHandle)) {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        return S_OK;
    }

    __declspec(dllexport) HRESULT SetInputFile(ISpRecoContext* pContext, ReadCallback readCallback) {
        ISpRecognizer* reco;
        HRESULT hr = pContext->GetRecognizer(&reco);
        if(FAILED(hr)) {
            return hr;
        }
        AudioStream* stream = new AudioStream(readCallback);

        hr = reco->SetInput(static_cast<ISpStreamFormat*>(stream), FALSE);
        if(FAILED(hr)) {
            reco->Release();
            stream->Release();
            return hr;
        }
        //reco->Release();
        return hr;
    }

    __declspec(dllexport) HRESULT SetInputStream(ISpRecoContext* pContext, ISpStreamFormat* stream) {
        ISpRecognizer* reco;
        HRESULT hr = pContext->GetRecognizer(&reco);
        if(FAILED(hr)) {
            return hr;
        }

        hr = reco->SetInput(stream, FALSE);
        if(FAILED(hr)) {
            reco->Release();
            return hr;
        }
        reco->Release();
        return hr;
    }

    __declspec(dllexport) HRESULT SetDeviceProperty_Bool(IMediaObject* pDMO, DWORD index, bool value) {
        IPropertyStore* pPS = NULL;
        HRESULT hr = pDMO->QueryInterface(IID_IPropertyStore, (void**)&pPS);
        if(FAILED(hr)) {
            return hr;
        }

        PROPVARIANT pvSourceMode;
        PropVariantInit(&pvSourceMode);    
        pvSourceMode.vt = VT_BOOL;
        pvSourceMode.boolVal = value ? VARIANT_TRUE : VARIANT_FALSE;

        PROPERTYKEY key = { { 0x6f52c567, 0x360, 0x4bd2, { 0x96, 0x17, 0xcc, 0xbf, 0x14, 0x21, 0xc9, 0x39 } }, index};

        auto res = pPS->SetValue(key, pvSourceMode);

        pPS->Release();

        return res;
    }

    __declspec(dllexport) HRESULT SetDeviceProperty_Int(IMediaObject* pDMO, DWORD index, int value) {
        IPropertyStore* pPS = NULL;
        HRESULT hr = pDMO->QueryInterface(IID_IPropertyStore, (void**)&pPS);
        if(FAILED(hr)) {
            return hr;
        }

        PROPVARIANT pvSourceMode;
        PropVariantInit(&pvSourceMode);    
        pvSourceMode.vt = VT_I4;
        pvSourceMode.iVal = value;

        PROPERTYKEY key = { { 0x6f52c567, 0x360, 0x4bd2, { 0x96, 0x17, 0xcc, 0xbf, 0x14, 0x21, 0xc9, 0x39 } }, index};

        auto res = pPS->SetValue(key, pvSourceMode);

        pPS->Release();

        return res;
    }

    __declspec(dllexport) HRESULT GetDeviceProperty_Bool(IMediaObject* pDMO, DWORD index, bool* value) {
        IPropertyStore* pPS = NULL;
        HRESULT hr = pDMO->QueryInterface(IID_IPropertyStore, (void**)&pPS);
        if(FAILED(hr)) {
            return hr;
        }

        PROPVARIANT pvSourceMode;
        PropVariantInit(&pvSourceMode);    

        PROPERTYKEY key = { { 0x6f52c567, 0x360, 0x4bd2, { 0x96, 0x17, 0xcc, 0xbf, 0x14, 0x21, 0xc9, 0x39 } }, index};

        auto res = pPS->GetValue(key, &pvSourceMode);
        pPS->Release();

        if(SUCCEEDED(res)) {
            *value = pvSourceMode.boolVal == VARIANT_TRUE;
        }
        return res;
    }

    __declspec(dllexport) HRESULT GetDeviceProperty_Int(IMediaObject* pDMO, DWORD index, int* value) {
        IPropertyStore* pPS = NULL;
        HRESULT hr = pDMO->QueryInterface(IID_IPropertyStore, (void**)&pPS);
        if(FAILED(hr)) {
            return hr;
        }

        PROPVARIANT pvSourceMode;
        PropVariantInit(&pvSourceMode);    

        PROPERTYKEY key = { { 0x6f52c567, 0x360, 0x4bd2, { 0x96, 0x17, 0xcc, 0xbf, 0x14, 0x21, 0xc9, 0x39 } }, index};

        auto res = pPS->GetValue(key, &pvSourceMode);

        pPS->Release();

        if(SUCCEEDED(res)) {
            *value = pvSourceMode.intVal;
        }

        return res;
    }

}
