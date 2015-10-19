import wpf

from System.Windows import Application, Window
from System.Windows import Annotations
import array

class MyWindow(Window):
    def __init__(self):
        wpf.LoadComponent(self, 'WpfApplication45.xaml')
    

if __name__ == '__main__':
	Application().Run(MyWindow())
        print('hello')

