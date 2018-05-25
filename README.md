# DC-Alpha-ILR-Processing-SF-POC

This repo contains the original service fabric experiments for ESFA Data Collections.

The purpose of the experiments were to work out how load of files and service fabric interact, how stateful actors could be used, how stateful services could be used, can we maximise CPU and could we determine how the scheduler worked and prioritised units of work.

The SF also hosts a rudimentary ILR uploading site to pump the data into the downstream processing elements. The file can also be “shredded” into multiple logical units of work. All of these facets together can be used to test performance in conjunction with strict control variables.

Please note that our results are not in this repo.  This repo is NOT maintained – our beta code is on github and is maintained.
