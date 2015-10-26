class SchoolMember:
	'''Represents any school member.'''
	def __init__(self,name,age):
		self.name = name
		self.age = age
		print ('Initialized Schoolmember: %s' %self.name)

	def tell(self):
		'''Tell my detail.'''
		print ('Name: "%s" Age: "%s"' %(self.name, self.age),)

class Teacher(SchoolMember):
	'''Represents a teacher.'''
	def __init__(self,name,age,salary):
		SchoolMember.__init__(self,name,age)
		self.salary = salary
		print ('Initialized Teacher: %s' %self.name)

	def tell(self):
		SchoolMember.tell(self)
		print ('Salary: %d' %self.salary)

class Student(SchoolMember):
	'''Represents a student.'''
	def __init__(self,name,age,marks):
		SchoolMember.__init__(self,name,age)
		self.marks = marks

	def tell(self):
		SchoolMember.tell(self)
		print ('Marks: %d' %self.marks)

t = Teacher('Mrs. Shrividya',40,3000)
s = Student('Swaroop',22,75)

print

members = [t,s]
for member in members:
	member.tell()