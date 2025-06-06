#!/usr/bin/env bash
DEST="/home/lampac"

# Become root
# sudo su -
apt-get update
apt-get install -y unzip curl
apt-get install -y libnss3-dev libgdk-pixbuf2.0-dev libgtk-3-dev libxss-dev libasound2
apt-get install -y xvfb
apt-get install -y coreutils

# Install .NET
if ! curl -L -k -o dotnet-install.sh https://dot.net/v1/dotnet-install.sh; then
   echo "Failed to download dotnet-install.sh. Exiting."
   exit 1
fi

chmod 755 dotnet-install.sh
./dotnet-install.sh --channel 6.0 --runtime aspnetcore --install-dir /usr/share/dotnet
ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Download zip
mkdir $DEST -p 
cd $DEST
if ! curl -L -k -o publish.zip https://github.com/immisterio/Lampac/releases/latest/download/publish.zip; then
   echo "Failed to download publish.zip. Exiting."
   exit 1
fi

unzip -o publish.zip
rm -f publish.zip

# custom settings
curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/init.conf > $DEST/init.conf
curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/lampainit-invc.my.js > $DEST/plugins/lampainit-invc.my.js
if ip addr | grep 192.168.10.; then
    echo "LAR subnet detected"
    curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/lampainit_lar.js > $DEST/plugins/lampainit.my.js
elif ip addr | grep 192.168.3.; then
    echo "UVA subnet detected"
    curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/lampainit_uva.js > $DEST/plugins/lampainit.my.js
else
    echo "Unknown subnet!"
fi

curl -s https://raw.githubusercontent.com/m0nty81/lampac/main/custom.settings/manifest.json > $DEST/module/manifest.json

# automatic updates
curl -k -s https://api.github.com/repos/immisterio/Lampac/releases/latest | grep tag_name | sed s/[^0-9]//g > $DEST/vers.txt
curl -k -s https://raw.githubusercontent.com/m0nty81/lampac/main/update.sh > $DEST/update.sh
chmod 755 $DEST/update.sh
#crontab -l | { cat; echo "$(shuf -i 10-55 -n 1) * * * * /bin/bash $DEST/update.sh"; } | crontab -

CRON_JOB="$(shuf -i 10-55 -n 1) * * * * /bin/bash $DEST/update.sh"
(crontab -l | grep -vF "/bin/bash $DEST/update.sh"; echo "$CRON_JOB") | crontab -

# init.conf
random_port=$(shuf -i 9000-12999 -n 1)
cat <<EOF > $DEST/init.conf
{
  "listenport": $random_port,
  "typecache": "mem",
  "mikrotik": true,
  "puppeteer": {
    "enable": false
  },
  "dlna": {
    "enable": false,
    "autoupdatetrackers": false
  },
  "weblog": {
    "enable": false
  },
  "serverproxy": {
    "verifyip": false,
    "cache": {
      "img": false,
      "img_rsize": false
    },
    "buffering": {
      "enable": false
    }
  }
}
EOF

# manifest.json
cat <<EOF > $DEST/module/manifest.json
[
  {
    "enable": true,
    "dll": "SISI.dll"
  },
  {
    "enable": true,
    "dll": "Online.dll"
  },
  {
    "enable": true,
    "dll": "DLNA.dll"
  },
  {
    "enable": false,
    "initspace": "TorrServer.ModInit",
    "dll": "TorrServer.dll"
  }
]
EOF

# Lampac.runtimeconfig.json
cat <<EOF > $DEST/Lampac.runtimeconfig.json
{
  "runtimeOptions": {
    "tfm": "net6.0",
    "frameworks": [
      {
        "name": "Microsoft.NETCore.App",
        "version": "6.0.0"
      },
      {
        "name": "Microsoft.AspNetCore.App",
        "version": "6.0.0"
      }
    ],
    "configProperties": {
      "System.GC.Server": false,
      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
      "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false,
      "System.GC.HeapHardLimit": 83886080
    }
  }
}
EOF

# Create service
echo ""
echo "Install service to /etc/systemd/system/lampac.service ..."
touch /etc/systemd/system/lampac.service && chmod 664 /etc/systemd/system/lampac.service
cat <<EOF > /etc/systemd/system/lampac.service
[Unit]
Description=Lampac
Wants=network.target
After=network.target
[Service]
WorkingDirectory=$DEST
ExecStart=/usr/bin/dotnet Lampac.dll
#ExecReload=/bin/kill -s HUP $MAINPID
#ExecStop=/bin/kill -s QUIT $MAINPID
Restart=always
LimitNOFILE=32000
[Install]
WantedBy=multi-user.target
EOF

if [ ! -f "$DEST/init.conf" ]; then
random_port=$(shuf -i 9000-12999 -n 1)
cat <<EOF > $DEST/init.conf
"listenport": $random_port
EOF
fi

# Enable service
systemctl daemon-reload
systemctl enable lampac

# update minor
echo -n "1" > $DEST/vers-minor.txt
/bin/bash $DEST/update.sh

# clear
cd $DEST
rm -rf merchant wwwroot/bwa
rm -rf runtimes/wi*
rm -rf runtimes/os*
rm -rf runtimes/linux-m*

# clear runtimes
case $(uname -m) in
    x86_64)
        rm -rf runtimes/linux-a*
        ;;
    armv7l)
        rm -rf runtimes/linux-arm64
		rm -rf runtimes/linux-x64
        ;;
    aarch64)
        rm -rf runtimes/linux-arm
		rm -rf runtimes/linux-x64
        ;;
    *)
        echo ""
        ;;
esac

# done
systemctl start lampac

# iptables drop
cat <<EOF > iptables-drop.sh
#!/bin/sh
echo "Stopping firewall and allowing everyone..."
iptables -F
iptables -X
iptables -t nat -F
iptables -t nat -X
iptables -t mangle -F
iptables -t mangle -X
iptables -P INPUT ACCEPT
iptables -P FORWARD ACCEPT
iptables -P OUTPUT ACCEPT
EOF

# Note
echo ""
echo "################################################################"
echo ""
echo "Have fun!"
echo ""
echo "http://IP:$random_port"
echo ""
echo "Please check/edit $DEST/init.conf params and configure it"
echo ""
echo "Then [re]start it as systemctl [re]start lampac"
echo ""
echo "Clear iptables if port $random_port is not available"
echo "bash $DEST/iptables-drop.sh"
echo ""
