FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG RID=linux-musl-x64
RUN apk add --upgrade --no-cache build-base clang zlib-dev
WORKDIR /src
COPY . .
WORKDIR "/src/src"
RUN dotnet publish "Dmx2Mqtt.csproj" -r $RID -c Release -o /out
RUN rm -f /out/*.dbg /out/*.Development.json && chmod +x /out/Dmx2Mqtt

FROM alpine AS final
WORKDIR /app
COPY --from=build /out/* .
ENTRYPOINT ["/app/Dmx2Mqtt"]
