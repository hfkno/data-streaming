#
# Basic Kafka installation
#

# Setup base Ubuntu installation
FROM ubuntu:trusty
MAINTAINER HFK

RUN apt-get update && apt-get dist-upgrade -y

# Zookeeper installation and startup
RUN apt-get install -y zookeeper
RUN apt-get clean && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*
# Log Zookeeper to onsole
RUN sed -i 's/ROLLINGFILE/CONSOLE/' /etc/zookeeper/conf/environment
# Start zookeeper cluster using linked bash script
ADD start.sh /usr/local/bin/
CMD ["/usr/local/bin/start.sh"]