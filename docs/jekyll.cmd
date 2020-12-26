@echo off
docker run --rm -it -p 4000:4000 -v "%CD%":"/docs" --workdir "/docs" jekyll/jekyll:latest jekyll %*
