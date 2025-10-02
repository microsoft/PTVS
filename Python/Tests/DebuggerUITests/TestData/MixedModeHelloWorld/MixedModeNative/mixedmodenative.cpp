#include "mixedmodenative.h"
#include <windows.h>

// Simple function we can set a native breakpoint on.
extern "C" int add_numbers(int a, int b) {
    int c = a + b; // native breakpoint target
    return c;
}
