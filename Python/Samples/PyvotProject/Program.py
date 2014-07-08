"""
A sample for working with the Pyvot library for Excel.
"""

from os import path

import xl

def main():
    """Loads and modifies a sample spreadsheet in Excel."""
    workbook_path = path.join(path.dirname(__file__), 'PyvotSample.xlsx')
    workbook = xl.Workbook(workbook_path)

    workbook.range("A1").set("Hello World!")

    def doubled(val):
        """Returns double of value passed in."""
        return 2 * val

    def alpha(val):
        """Returns letter at specified position in alphabet."""
        return "abcdefgh"[int(val) - 1]

    xl.map(doubled, workbook.get("Values"))
    xl.map(alpha, workbook.get("doubled"))

    print(workbook.range("C3:E7").get())

if __name__ == '__main__':
    main()
