# Build context PHẢI là thư mục gốc của repo (chứa cả SecureChat.Server và
# SecureChat.Shared), không phải thư mục SecureChat.Server riêng - vì project
# này có ProjectReference tới SecureChat.Shared.
#
# Build (từ thư mục gốc repo):
#   docker build -f SecureChat.Server/Dockerfile -t securechat-server .
# Chạy thử local:
#   docker run -p 8080:8080 -e PORT=8080 -e ConnectionStrings__Default="..." securechat-server

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file .csproj trước để cache layer restore (nhanh hơn khi chỉ đổi code)
COPY SecureChat.Server/SecureChat.Server.csproj SecureChat.Server/
COPY SecureChat.Shared/SecureChat.Shared.csproj SecureChat.Shared/
RUN dotnet restore SecureChat.Server/SecureChat.Server.csproj

# Copy toàn bộ source rồi build/publish
COPY SecureChat.Server/ SecureChat.Server/
COPY SecureChat.Shared/ SecureChat.Shared/
RUN dotnet publish SecureChat.Server/SecureChat.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Platform (Render/Fly.io/Railway) sẽ set biến môi trường PORT, Program.cs đọc
# biến này để bind đúng cổng. EXPOSE chỉ là khai báo metadata, không bắt buộc
# khớp chính xác với PORT thật lúc runtime.
EXPOSE 8080

ENTRYPOINT ["dotnet", "SecureChat.Server.dll"]
