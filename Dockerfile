# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution + project files trước để tận dụng cache khi restore
COPY SalesWeb.sln ./
COPY SalesWeb.Shared/*.csproj ./SalesWeb.Shared/
COPY SalesWeb.Server/*.csproj ./SalesWeb.Server/
COPY SalesWeb.Client/*.csproj ./SalesWeb.Client/
RUN dotnet restore SalesWeb.sln

# Copy toàn bộ source và publish (Server đã reference Client Blazor nên wwwroot được đóng gói kèm)
COPY . .
RUN dotnet publish SalesWeb.Server/SalesWeb.Server.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# SQLite lưu tại /app/data — mount volume để giữ dữ liệu khi recreate container
ENV ASPNETCORE_URLS=http://+:8080 \
    DATA_DIR=/app/data

EXPOSE 8080
VOLUME /app/data

ENTRYPOINT ["dotnet", "SalesWeb.Server.dll"]
