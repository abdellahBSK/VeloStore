# 🚀 Quick Start Guide - VeloStore

## Prerequisites

Before running VeloStore, ensure you have:

### ✅ **1. .NET 8.0 SDK**
- Check: `dotnet --version` (should show 8.x.x)
- Download: https://dotnet.microsoft.com/download/dotnet/8.0

### ✅ **2. SQL Server**
- **Local SQL Server** (Express or Developer Edition)
- Or **SQL Server in Docker**
- Connection string in `appsettings.json`: `Server=.;Database=VeloStoreDB;...`

### ✅ **3. Redis Server** (Required for Caching)
- **Option A: Local Redis**
  - Windows: Download from https://github.com/microsoftarchive/redis/releases
  - Or use Docker: `docker run -d -p 6379:6379 redis`
  
- **Option B: Redis Cloud** (for production)
  - Update connection string in `appsettings.json`

### ✅ **4. Database Migrations**
Run migrations to create database schema:
```bash
dotnet ef database update
```

---

## 🏃 Running the Application

### **Method 1: Visual Studio / VS Code**
1. Open the solution file: `VeloStore.slnx`
2. Press `F5` or click "Run"
3. Application will start on: `https://localhost:5001` or `http://localhost:5000`

### **Method 2: Command Line**
```bash
cd VeloStore
dotnet run
```

The application will start and show:
```
Now listening on: https://localhost:5001
Now listening on: http://localhost:5000
```

---

## ⚠️ Common Issues & Solutions

### **Issue 1: Redis Connection Failed**
**Error**: `Unable to connect to Redis server`

**Solutions**:
1. **Start Redis**:
   ```bash
   # Using Docker
   docker run -d -p 6379:6379 --name redis redis
   
   # Or install Redis for Windows
   # Download from: https://github.com/microsoftarchive/redis/releases
   ```

2. **Check Redis is running**:
   ```bash
   # Test connection
   redis-cli ping
   # Should return: PONG
   ```

3. **Update connection string** in `appsettings.json` if Redis is on different host/port

### **Issue 2: Database Connection Failed**
**Error**: `Cannot open database "VeloStoreDB"`

**Solutions**:
1. **Ensure SQL Server is running**
   - Check SQL Server service in Services (services.msc)
   - Or use SQL Server Management Studio

2. **Create database manually**:
   ```sql
   CREATE DATABASE VeloStoreDB;
   ```

3. **Run migrations**:
   ```bash
   dotnet ef database update
   ```

4. **Update connection string** in `appsettings.json` if needed

### **Issue 3: EF Core Tools Not Found**
**Error**: `dotnet-ef command not found`

**Solution**:
```bash
dotnet tool install --global dotnet-ef
```

### **Issue 4: Port Already in Use**
**Error**: `Address already in use`

**Solution**:
1. Change port in `Properties/launchSettings.json`
2. Or stop the application using the port

---

## 🔧 Configuration

### **Database Connection**
Edit `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=VeloStoreDB;..."
  }
}
```

### **Redis Connection**
Edit `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

For remote Redis:
```json
{
  "ConnectionStrings": {
    "Redis": "your-redis-server:6379,password=your-password"
  }
}
```

---

## 🧪 Testing the Application

### **1. Home Page**
- Navigate to: `https://localhost:5001`
- Should see product catalog (if products exist in database)

### **2. Register/Login**
- Click "Register" to create account
- Or use existing account to login

### **3. Add to Cart**
- Browse products
- Click "Add to Cart" (requires login)
- View cart at: `https://localhost:5001/Cart`

### **4. Search & Filter**
- Use search box on home page
- Filter by price range
- Sort products

---

## 📊 Verifying Caching Works

### **Check Redis Cache**
```bash
# Connect to Redis
redis-cli

# List all keys
KEYS VeloStore:*

# Check home products cache
GET VeloStore:home_products_redis

# Check user cart
GET VeloStore:cart:USER_ID
```

### **Monitor Application Logs**
The application logs cache operations:
- `"Home products retrieved from memory cache"` - L1 cache hit
- `"Home products retrieved from Redis cache"` - L2 cache hit
- `"Cache miss - Loading home products from database"` - L3 (DB query)

---

## 🛑 Stopping the Application

### **In Terminal**
Press `Ctrl+C` to stop

### **In Visual Studio**
Click "Stop" button or press `Shift+F5`

---

## 📝 Next Steps

1. **Seed Database** - Add sample products
2. **Create Admin User** - Set up admin account
3. **Configure Production** - Update settings for deployment
4. **Add Products** - Use admin panel (when implemented)

---

## 🆘 Need Help?

- Check application logs in console output
- Review `COMPREHENSIVE_PROJECT_ANALYSIS.md` for architecture details
- Review `CACHING_STRATEGY.md` for caching information

---

**Happy Coding! 🚀**

