class SchoolMember:
	'''Representsany school member.'''
	def __init__(self,name,age):
		self.name = name
		self.age = age
		print ('Initialized Schoolmember: %s' %self.name)

	def tell(self):
		'''Tell my detail.'''
		print ('Name: "%s" Age: "%s"' %(self.name, self.age),)
            
def sayHi():
	print ('Hi, this is mymodule speaking.')

version='0.1'