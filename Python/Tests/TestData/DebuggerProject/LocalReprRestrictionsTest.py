s = "01234567890123456789012345678901234567890123456789"
sa1 = [s]
sa2 = [sa1, sa1]
sa3 =  [sa2, sa2, sa2]
sa4 =  [sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3, sa3]

da1 = {s : s}
da2 = {s : da1, '1' : da1}
da3 = {s : da2, '1' : da2, '2' : da2}
da4 = {s : da3, '01': da3, '02': da3, '03': da3, '04': da3, '05': da3, '06': da3, '07': da3, '08': da3, '09': da3, '10': da3, '11': da3, '12': da3, '13': da3, '14': da3, '15': da3, '16': da3, '17': da3, '18': da3, '19': da3, '20': da3}

n = 12345678901234567890123456789012345678901234567890
na1 = [n]
na2 = [na1, na1]
na3 =  [na2, na2, na2]
na4 =  [na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3, na3]

class my_class:
    def __repr__(self):
        return "my_class: 0123456789012345678901234567890123456789"
c = my_class()
ca1 = [c]
ca2 = [ca1, ca1]
ca3 =  [ca2, ca2, ca2]
ca4 =  [ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3, ca3]

print('done')
