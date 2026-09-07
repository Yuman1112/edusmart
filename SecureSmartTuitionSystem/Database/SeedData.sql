/* ============================================================================
   Seed data — minimum rows needed to exercise every use case without a UI.
   Run after Schema.sql.
   ============================================================================ */

-- One Admin identity (password hash normally set by the Authentication module)
INSERT INTO dbo.AspNetUsers (Id, UserName, Email, Role, IsActive)
VALUES ('admin-seed-0001', 'admin', 'admin@tuitioncentre.test', 'Admin', 1);

-- Subjects with monthly fee schedule (used by FeeCalculationService)
INSERT INTO dbo.Subjects (SubjectName, FormYear, MonthlyFee, IsActive) VALUES
 ('Mathematics', 'Form 4', 180.00, 1),
 ('Additional Mathematics', 'Form 4', 200.00, 1),
 ('Physics', 'Form 4', 190.00, 1),
 ('Chemistry', 'Form 4', 190.00, 1),
 ('English', 'Form 4', 150.00, 1);

-- A class session for Mathematics
INSERT INTO dbo.ClassSessions (SubjectId, TutorId, DayOfWeek, StartTime, EndTime, Venue, MinCapacity, MaxCapacity, Status)
VALUES (1, NULL, 2, '18:00', '20:00', 'Room A1', 5, 25, 'Active');

-- Chatbot FAQ knowledge base (rule-based menu + keyword layer, Ch.2.3)
INSERT INTO dbo.ChatbotFaqs (MenuCategory, Question, Answer, Language, DisplayOrder, IsActive) VALUES
 ('Fees', 'How much is the monthly fee?', 'Monthly fees depend on the subjects enrolled. Check "View Monthly Bill" for your child''s exact amount.', 'EN', 1, 1),
 ('Fees', 'What happens if I pay late?', 'A grace period of up to 7 days is given. After that a warning and email are sent, and the account is restricted to view-only until payment is received.', 'EN', 2, 1),
 ('Enrollment', 'How do I enroll a new student?', 'Browse available classes, click "Enroll Now", and complete the enrollment form. You will receive a payment link once approved.', 'EN', 1, 1),
 ('Schedule', 'How do I check the class schedule?', 'Log in and open "Schedule Management" or "Browse Available Classes" to see all class timings.', 'EN', 1, 1);

INSERT INTO dbo.ChatbotKeywords (ChatbotFaqId, Keyword, MatchMode) VALUES
 (1, 'fee', 'Any'), (1, 'price', 'Any'), (1, 'cost', 'Any'),
 (2, 'late', 'Any'), (2, 'overdue', 'Any'), (2, 'penalty', 'Any'),
 (3, 'enroll', 'Any'), (3, 'register', 'Any'), (3, 'signup', 'Any'),
 (4, 'schedule', 'Any'), (4, 'timetable', 'Any'), (4, 'class time', 'All');

PRINT 'Seed data inserted.';
GO
