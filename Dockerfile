# Stage 1: Build (ใช้ SDK 9.0)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy ไฟล์โปรเจกต์มา Restore ก่อน (ช่วยให้ Build ครั้งต่อไปเร็วขึ้น)
COPY ["Buffet_Restaurant_Management_System_API.csproj", "./"]
RUN dotnet restore "Buffet_Restaurant_Management_System_API.csproj"

# Copy ไฟล์ทั้งหมดและ Publish
COPY . .
RUN dotnet publish "Buffet_Restaurant_Management_System_API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime (ใช้ ASP.NET 9.0 ขนาดเล็กประหยัด RAM)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
RUN apt-get update && apt-get install -y icu-devtools
WORKDIR /app
COPY --from=build /app/publish .

# Railway จะส่ง Port มาให้ผ่าน ENV "PORT"
# เราสั่งให้ ASP.NET Core ฟังที่ Port นั้นโดยตรง
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Buffet_Restaurant_Management_System_API.dll"]