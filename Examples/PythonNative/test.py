print("Made it to the main script")

def loopforever():
    while True:
        c = input('Enter a char')
        if len(c) == 0:
            return
        print('Char is ', c)


def callloop():
    loopforever()
    print("looping again")
    loopforever()


callloop()

