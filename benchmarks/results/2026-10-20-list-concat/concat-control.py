def check():
    child = []
    child.append(child)
    left = [child]
    right = [child, 7]
    joined = left + right
    joined.append(9)
    right.append(8)
    empty = []
    fresh = empty + empty
    return [len(left), len(right), len(joined), joined[0] is child,
            joined[0] is joined[1], joined is left or joined is right,
            child[0] is child, fresh is empty]


actual = check()
assert actual == [1, 3, 4, True, True, False, True, False]
print(actual)
