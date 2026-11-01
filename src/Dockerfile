FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG RID=linux-musl-x64
RUN apk add --upgrade --no-cache build-base clang zlib-dev curl linux-headers

# Alpine has no static alsa-lib, and the binary is statically linked
ARG ALSA_VERSION=1.2.15.3
ARG ALSA_SHA256=7b079d614d582cade7ab8db2364e65271d0877a37df8757ac4ac0c8970be861e
RUN curl -fsSL -o /tmp/alsa.tar.bz2 https://www.alsa-project.org/files/pub/lib/alsa-lib-$ALSA_VERSION.tar.bz2 \
	&& echo "$ALSA_SHA256  /tmp/alsa.tar.bz2" | sha256sum -c - \
	&& tar xjf /tmp/alsa.tar.bz2 -C /tmp \
	&& cd /tmp/alsa-lib-$ALSA_VERSION \
	&& ./configure --prefix=/opt/alsa --with-configdir=/usr/share/alsa --enable-static --disable-shared \
		--disable-python --disable-topology --disable-ucm --disable-alisp --disable-old-symbols \
	&& make -j$(nproc) install \
	&& rm -rf /tmp/alsa*

WORKDIR /src
COPY . .
RUN make -C src/Native static ALSA_CFLAGS=-I/opt/alsa/include
WORKDIR "/src/src"
RUN dotnet publish "Dmx2Mqtt.csproj" -r $RID -c Release -o /out \
	-p:DmxAudioLibrary=/src/src/Native/build/libdmxaudio.a -p:AlsaLibrary=/opt/alsa/lib/libasound.a
RUN rm -f /out/*.dbg /out/*.Development.json && chmod +x /out/Dmx2Mqtt

FROM alpine AS final
WORKDIR /app
# alsa-lib reads its configuration at run time
COPY --from=build /usr/share/alsa /usr/share/alsa
COPY --from=build /out/* .
ENTRYPOINT ["/app/Dmx2Mqtt"]
