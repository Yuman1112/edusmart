This folder is where your SecureSmartTuitionSystemDb.mdf file goes.

It is NOT included in this package because a .mdf is a live SQL Server
database file, not something that can be shipped as source code.

To create it:
  1. In Visual Studio's Solution Explorer, right-click this App_Data folder
     -> Add -> New Item... -> search "SQL Server Database" -> name it
     SecureSmartTuitionSystemDb.mdf -> Add.
  2. It will appear under View -> Server Explorer -> Data Connections.
  3. Right-click that connection -> New Query -> paste in
     Database/Schema.sql -> Execute.
  4. Repeat step 3 with Database/SeedData.sql.

Once SecureSmartTuitionSystemDb.mdf exists in this folder, the connection
string in appsettings.json (or your User Secrets) already points at it via
the |DataDirectory| macro, so no further configuration is needed.
