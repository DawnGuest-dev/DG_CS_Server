# 1. 빌드 스테이지 (SDK 포함된 이미지 사용)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 프로젝트 파일 복사 (캐시 최적화를 위해 .csproj만 먼저 복사)
COPY ["Server/Server.csproj", "Server/"]
COPY ["Common/Common.csproj", "Common/"]

# 패키지 복원 (NuGet Restore)
RUN dotnet restore "Server/Server.csproj"

# 전체 소스 복사
COPY . .

# 빌드 및 게시 (Publish)
WORKDIR "/src/Server"
RUN dotnet publish "Server.csproj" -c Release -o /app/publish

# 2. 실행 스테이지 (가벼운 Runtime 이미지만 사용)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# 빌드 스테이지에서 생성된 파일 복사
COPY --from=build /app/publish .

# 설정 파일 복사 (혹시 publish에 포함 안 됐을 경우를 대비)
# (Visual Studio 설정에서 '출력 디렉터리로 복사' 되어 있으면 위에서 복사됨)
COPY Server/ServerConfig_1.json .
COPY Server/ServerConfig_2.json .
COPY Server/Data/Config/PlayerStat.json ./Data/Config/

# 실행 명령어 (실행 시 인자로 1, 2 등을 받음)
ENTRYPOINT ["dotnet", "Server.dll"]