import cpp_mod
with_cmp = cpp_mod.CppObjWithCompare()
without_cmp = cpp_mod.CppObjWithoutCompare()

with_cmp == without_cmp
without_cmp == with_cmp

with_cmp != without_cmp
without_cmp != with_cmp

with_cmp < without_cmp
without_cmp < with_cmp

with_cmp <= without_cmp
without_cmp <= with_cmp

with_cmp > without_cmp
without_cmp > with_cmp

with_cmp >= without_cmp
without_cmp >= with_cmp
