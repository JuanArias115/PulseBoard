# PulseBoard server setup command

Run this from the local Mac when ready to enter the server sudo password:

```bash
cd /Users/juanarias/Documents/Fuentes/PulseBoard
PUBLIC_KEY="$(cat ~/.ssh/pulseboard_deploy.pub)"
ssh -t -i ~/.ssh/bakeryflow_deploy juan@217.216.92.208 \
  "PUBLIC_KEY='$PUBLIC_KEY' bash -s" < deploy/scripts/prepare-server.sh
```

Then connect the existing public Nginx container to the PulseBoard ingress network after the first deploy creates it:

```bash
ssh -t -i ~/.ssh/bakeryflow_deploy juan@217.216.92.208 \
  'docker network connect pulseboard_ingress deliciasbakery-web-1 || true'
```

The public proxy configuration lives at:

```text
/opt/deliciasBakery/nginx.conf
```

Append the contents of:

```text
deploy/nginx/delicias-proxy-pulseboard.conf
```

Then issue certificates and restart the proxy.
