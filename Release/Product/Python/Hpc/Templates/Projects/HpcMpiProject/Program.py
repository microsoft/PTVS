from mpi4py import MPI
comm = MPI.COMM_WORLD

if comm.Get_rank() == 0:
    pass
else:
    pass

MPI.Finalize()