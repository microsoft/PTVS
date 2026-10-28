def add(left, right=2):
    return left + right

first = 1
result = add(first)
keyword_result = add(result, right=4)
native_result = len([first, result, keyword_result])
print(result, keyword_result, native_result)

class LessThan:
    def __lt__(self, other):
        return True

comparison_result = LessThan() < LessThan()
assert comparison_result
