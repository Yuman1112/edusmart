# Secure Smart Tuition System — Coding & Database Deliverable

This is the **backend implementation (C# / ASP.NET Core MVC / EF Core) and SQL Server database**
for the five modules covered in Yuman Ang's FYP report:

> Class & Enrollment Management · Payment & Billing · Communication & Announcement ·
> Reporting & Analytics · System Security

Per the brief, **UML diagrams, the ERD, and UI design (Razor Views/screens) are intentionally
excluded** — this package is the controllers, services, data model, and database schema only.
Everything here is written to be traceable back to a specific section, table, or functional
requirement in the report (see the traceability table below and the `//` / `///` comments
throughout the code, most of which cite the exact chapter/table).

## Tech stack (matches Ch.2 "Adopted Approach")

| Layer | Technology | Report reference |
|---|---|---|
| Framework | ASP.NET Core MVC (C#, .NET 8) | Ch.2.4 "ASP.NET Core MVC Framework" |
| ORM / Database access | Entity Framework Core (Code-First) | Ch.2.4 "Database Integration Using SQL Server and EF Core" |
| Database | SQL Server | Ch.2.4, Ch.2.6 "Database" comparison |
| Encryption | AES-256 via `System.Security.Cryptography` | Ch.2.3 (Payment & Billing) |
| Duplicate detection | Exact match + Jaro-Winkler (birthdate-blocked) | Ch.2.2 |
| Chatbot | Rule-based menu + keyword matching | Ch.2.3 (Communication) |
| Reporting | OLAP-style `GROUP BY` / `ROLLUP` against SQL Server | Ch.2.4 (Reporting) |
| Security | Input validation, output encoding, CSRF, parameterised queries, session mgmt, file validation, activity logging, structured error handling | Ch.2.5 |

## Project layout

```
SecureSmartTuitionSystem/
├── Database/
│   ├── Schema.sql          <- full T-SQL DDL (the "database" deliverable)
│   └── SeedData.sql        <- minimum rows to exercise every use case
├── Models/                 <- EF Core entities (1:1 with Schema.sql)
├── Data/AppDbContext.cs    <- EF Core DbContext, relationships, AES + enum conversions
├── Services/                <- business logic (one interface + implementation per concern)
├── ViewModels/RequestModels.cs <- input DTOs with Data Annotations (server-side validation)
├── Controllers/             <- one controller per module, one action per use case table
├── BackgroundServices/      <- scheduled jobs (monthly billing, overdue rules, deadline alerts)
├── Filters/                 <- [Audit] attribute + activity-log filter, current-user helpers
├── Middleware/               <- global exception handler
├── Program.cs                <- composition root / startup
└── appsettings.json
```

## Getting started

1. **Create the database (App_Data / .mdf approach).**
   - In Visual Studio's Solution Explorer, right-click the `App_Data` folder -> **Add -> New
     Item...** -> search **"SQL Server Database"** -> name it `SecureSmartTuitionSystemDb.mdf`
     -> **Add**. (See `App_Data/README.txt`.)
   - Open **View -> Server Explorer**, find the new connection under **Data Connections**,
     right-click it -> **New Query** -> paste in `Database/Schema.sql` -> **Execute**.
   - Repeat with `Database/SeedData.sql`.
   - `appsettings.json`'s connection string already points at this file via the `|DataDirectory|`
     macro (wired up explicitly in `Program.cs`, since ASP.NET Core — unlike classic ASP.NET —
     doesn't resolve `|DataDirectory|` to `App_Data` automatically), so no extra configuration is
     needed for the database connection itself.
   - Prefer LocalDB-by-name instead of a `.mdf` file, or a full SQL Server instance? Just replace
     `ConnectionStrings:DefaultConnection` in `appsettings.json` (or override it via User Secrets)
     with `Server=(localdb)\MSSQLLocalDB;Database=SecureSmartTuitionSystemDb;Trusted_Connection=True;TrustServerCertificate=True` or your own server's connection string — nothing else in the
     project needs to change either way.
2. **Set the AES key/IV** via User Secrets rather than committing them:
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "Security:AesKeyBase64" "<base64 of 32 random bytes>"
   dotnet user-secrets set "Security:AesIvBase64" "<base64 of 16 random bytes>"
   ```
   Generate them quickly with `openssl rand -base64 32` / `openssl rand -base64 16`, or in Visual
   Studio: right-click the project -> **Manage User Secrets** -> paste the two values into the
   `secrets.json` that opens.
3. **Restore & run**:
   ```bash
   dotnet restore
   dotnet run
   ```
   If `ClosedXML`/`QuestPDF`'s pinned versions in the `.csproj` are no longer the latest on
   NuGet by the time you build this, run `dotnet add package ClosedXML` / `dotnet add package
   QuestPDF` without a version to pick up the current release.
4. **No login screen yet? Use the DevAuth testing helper.** Because the User Authentication &amp;
   RBAC Module (login/registration) is the teammate's module, this project has no `/Account/Login`
   page — visiting `/` straight after `dotnet run`/F5 will 404, since every Controller requires
   `[Authorize]` and there is nothing to redirect to yet. `Controllers/DevAuthController.cs` is a
   throwaway stand-in just for local testing/demo: open `/DevAuth/LoginAsAdmin` (or
   `LoginAsGuardian`/`LoginAsStudent`/`LoginAsTutor`) in the browser to get a signed-in session,
   then browse straight to any endpoint that role can reach (e.g. `/Class/Browse`,
   `/Reporting/Dashboard`). Delete this file once the real login module is wired in — nothing
   else depends on it.
5. **Wire in the teammate's modules.** Three integration points are stubbed (clearly marked
   `[STUB - ... Module]` in the code) because they belong to the other five modules of the wider
   Secure Smart Tuition System, not this report:
   - `ApplicationUser` / cookie authentication (`Program.cs`) → replace with the real
     `AddIdentity<...>()` setup from the **User Authentication & RBAC Module**.
   - `Tutor` → **Tutor Management Module**.
   - `Assignment` / `AssignmentSubmission` → **Learning & Assessment Module** (this report only
     *reads* these tables, for deadline notifications and the submission-rate report; see the
     `NotifyGradedAsync` / `NotifyMaterialUploadedAsync` hooks in
     `BackgroundServices/NotificationSchedulerService.cs` that module should call directly).
   - `IPaymentGatewayClient` → swap `MockPaymentGatewayClient` for a real gateway SDK
     (Stripe/Billplz/iPay88/etc.) before going live; no other code needs to change.

## Traceability: report → code

### Class & Enrollment Management
| Report item | File |
|---|---|
| Ch.2.2 Duplicate-record validation (exact match + Jaro-Winkler, birthdate blocking) | `Services/DuplicateRecordService.cs` |
| Table 4.1 Create Class Session | `Controllers/ClassController.cs :: CreateSession` |
| Table 4.2 Manage Class Schedule (Update/Cancel/Reschedule) | `Controllers/ClassController.cs :: UpdateSchedule/CancelSession/RescheduleSession` |
| Table 4.3 Browse Available Classes | `Controllers/ClassController.cs :: Browse/Details` |
| Table 4.4 Submit Enrollment Request | `Controllers/EnrollmentController.cs :: Submit` |
| Table 4.5 Approve/Reject Enrollment | `Controllers/EnrollmentController.cs :: Decide` |
| Table 4.6 View Enrollment History | `Controllers/EnrollmentController.cs :: History` |
| Table 4.12 Add/Drop Subject Billing Adjustment | `Controllers/EnrollmentController.cs :: RequestSubjectChange` |

### Payment & Billing
| Report item | File |
|---|---|
| Ch.2.3 AES-256 encryption of payment/billing columns | `Services/AesEncryptionService.cs`, `Data/AppDbContext.cs` (value converters) |
| Ch.2.3 PCI-DSS-aligned tokenisation | `Services/IPaymentGatewayClient.cs` + `Payment.GatewayTokenEncrypted` |
| Ch.2.3 Automated fee calculation | `Services/FeeCalculationService.cs` |
| Table 4.7 Enrollment Fee Calculation | `Controllers/PaymentController.cs :: RecalculateFee`, `EnrollmentController.cs :: BuildEnrollmentInvoiceAsync` |
| Table 4.8 View Monthly Bill | `Controllers/PaymentController.cs :: CurrentBill` |
| Table 4.9 Make Payment | `Controllers/PaymentController.cs :: Pay` |
| Table 4.10 View Receipt | `Controllers/PaymentController.cs :: Receipt` |
| Table 4.11 Payment Status Tracking | `Controllers/PaymentController.cs :: Status` |
| Table 4.13 Payment History Viewing | `Controllers/PaymentController.cs :: History` |
| Monthly bill auto-generation (1st of month) + overdue grace period/suspension/reactivation | `BackgroundServices/BillingSchedulerService.cs` |

### Communication & Announcement
| Report item | File |
|---|---|
| Ch.2.3 Rule-based menu + keyword-pattern chatbot | `Services/ChatbotService.cs` |
| Ch.2.3 Rule-based notification triggering (once/day) | `Services/NotificationService.cs` |
| Table 4.14 AI Chatbot FAQ | `Controllers/CommunicationController.cs :: ChatbotMenu/Ask/Escalate` |
| Table 4.15 Send and Receive Messages | `Controllers/CommunicationController.cs :: Send` |
| Table 4.16 Chat History Management | `Controllers/CommunicationController.cs :: History/Archive/Delete` |
| Table 4.17 Assignment Reminder Notifications | `BackgroundServices/NotificationSchedulerService.cs :: SendUpcomingDeadlineRemindersAsync` |
| Table 4.18 Deadline & Late Submission Alerts | `BackgroundServices/NotificationSchedulerService.cs :: SendOverdueSubmissionAlertsAsync` |
| Table 4.19 Notification Read/Unread Tracking | `Controllers/NotificationController.cs :: Index/SetRead/MarkAllRead` |
| Announcement broadcast + delivery/read tracking | `Controllers/NotificationController.cs :: Broadcast/MarkAnnouncementRead/AnnouncementStatus` |

### Reporting & Analytics
| Report item | File |
|---|---|
| Ch.2.4 OLAP-style `GROUP BY`/`ROLLUP` aggregation | `Services/ReportingService.cs` |
| Table 4.20 Dashboard Visualization | `Controllers/ReportingController.cs :: Dashboard` |
| Table 4.21 Data Filtering by Subject/Class | `Controllers/ReportingController.cs :: FilterOptions` |
| Table 4.22 Student Performance Summary | `Controllers/ReportingController.cs :: StudentPerformance` |
| Table 4.23 Assignment Submission Rate Report | `Controllers/ReportingController.cs :: SubmissionRate` |
| Table 4.24 Income Report | `Controllers/ReportingController.cs :: Income` |
| Table 4.25 Class Progress Overview | `Controllers/ReportingController.cs :: ClassProgress` |
| Table 4.26 Report Export (PDF/Excel) | `Controllers/ReportingController.cs :: Export`, `Services/ExportService.cs` |

### System Security
| Report item | File |
|---|---|
| Table 4.27 Input Validation | Data Annotations on every `ViewModels/RequestModels.cs` DTO + `ModelState.IsValid` checks |
| Table 4.28 XSS Protection | Razor's automatic output encoding (framework-level; see comments on `Message.Body`) |
| Table 4.29 CSRF Protection | `[ValidateAntiForgeryToken]` on every state-changing action; `Program.cs` Antiforgery config |
| Table 4.30 Session Management | `Program.cs` session/cookie configuration |
| Table 4.31 File Upload Security | `Services/FileValidationService.cs` |
| Table 4.32 Activity Logging | `Services/ActivityLogService.cs`, `Filters/AuditActionFilter.cs`, `[Audit]` attribute |
| Table 4.33 Error Handling | `Middleware/ExceptionHandlingMiddleware.cs` |
| Parameterised queries (SQL Injection) | Inherent to every EF Core LINQ query; the one raw-SQL query (`ReportingService.GetIncomeReportAsync`) uses parameterised `SqlQuery<T>($"...")` interpolation, never string concatenation |
| Role-Based Access Control | `[Authorize(Roles = "...")]` on every Controller/action per role in the use case tables |

## What was deliberately left out

- **Razor Views / `wwwroot` front-end** — UI Design is explicitly excluded from this deliverable.
  Every action returns `Json(...)`/`Ok(...)`/`File(...)` so a UI (or a thin API client) can be
  built against it directly.
- **UML class/use-case/activity diagrams and the ERD image** — excluded per the brief; the
  Functional Requirements → Use Case → Code traceability table above serves the same purpose in
  text form.
- **Real payment gateway, SMTP provider, and SignalR real-time push** — each has a clearly
  marked seam (`IPaymentGatewayClient`, the `// TODO integration point` comment in
  `BillingSchedulerService`, and `Message.IsDeliveredRealtime`) rather than a fake
  implementation dressed up as a real one.
