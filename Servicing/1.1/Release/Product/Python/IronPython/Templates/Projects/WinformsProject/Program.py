import clr
clr.AddReference('System.Windows.Forms')

from System.Windows.Forms import *

class MyForm(Form):
    def __init__(self):
         button = Button()
         button.Text = 'Click Me'
         self.Controls.Add(button)


form = MyForm()
Application.Run(form)
