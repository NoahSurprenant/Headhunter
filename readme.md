Building docker image:  
docker build . -f Headhunter.API/Dockerfile -t headhunter  

Saving docker image:  
docker save headhunter -o headhunter.tar  