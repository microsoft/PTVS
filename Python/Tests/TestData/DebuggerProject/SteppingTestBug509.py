def triangular_number(a): return a + triangular_number(a-1) if a > 0 else 0

print(triangular_number(3))