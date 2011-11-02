import os.path
import xl

sample_workbook_path = os.path.join(os.path.dirname(__file__), 'PyvotSample.xlsx')

def main():
    w = xl.Workbook(sample_workbook_path)

    w.range("A1").set("Hello World!")

    def doubled(x): return 2*x
    def alpha(x): return "abcdefgh"[int(x) - 1]
    xl.map(doubled, w.get("Values"))
    xl.map(alpha, w.get("doubled"))

    print w.range("C3:E7").get()

if __name__ == '__main__': main()