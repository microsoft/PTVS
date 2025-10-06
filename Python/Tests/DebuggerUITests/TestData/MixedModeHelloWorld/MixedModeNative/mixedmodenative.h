#pragma once

#ifdef _WIN32
  #define MIXEDMODENATIVE_EXPORT __declspec(dllexport)
#else
  #define MIXEDMODENATIVE_EXPORT
#endif

extern "C" MIXEDMODENATIVE_EXPORT int add_numbers(int a, int b);
