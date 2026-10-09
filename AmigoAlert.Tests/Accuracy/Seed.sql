-- Reference data for AccuracyTests: six users, each with one active emergency contact who has granted consent.
-- Re-runnable: removes these fixed users, and every row hanging off them, before inserting them again. Touches nothing else.
-- Per-scenario state (schedule, status, check-ins) is set by the test itself, relative to the run.

DECLARE @Users TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Users (Id) VALUES
	('a1000000-0000-0000-0000-000000000001'),
	('a1000000-0000-0000-0000-000000000002'),
	('a1000000-0000-0000-0000-000000000003'),
	('a1000000-0000-0000-0000-000000000004'),
	('a1000000-0000-0000-0000-000000000005'),
	('a1000000-0000-0000-0000-000000000006');

DELETE FROM [Sms] WHERE [EmergencyContactId] IN (SELECT [Id] FROM [EmergencyContact] WHERE [UserId] IN (SELECT [Id] FROM @Users));
DELETE FROM [StatusLog] WHERE [UserId] IN (SELECT [Id] FROM @Users);
DELETE FROM [CheckIn] WHERE [UserId] IN (SELECT [Id] FROM @Users);
DELETE FROM [EmergencyContact] WHERE [UserId] IN (SELECT [Id] FROM @Users);
DELETE FROM [User] WHERE [Id] IN (SELECT [Id] FROM @Users);

-- StatusId 1 = Okay. The user's own phone differs from the contact's, so a test can tell who an SMS went to.
INSERT INTO [User] ([Id], [Name], [Email], [Phone], [Address], [Landmarks], [StatusId], [AcceptedTermsTime], [AcceptedTermsVersion],
	[Notification1Time], [Notification2Time])
VALUES
	('a1000000-0000-0000-0000-000000000001', 'Accuracy User 1', 'accuracy1@amigoalert.test', '+593980000001', '1 Test St., Cotacachi, Ecuador', NULL, 1, SYSDATETIMEOFFSET(), '1.0', NULL, NULL),
	('a1000000-0000-0000-0000-000000000002', 'Accuracy User 2', 'accuracy2@amigoalert.test', '+593980000002', '2 Test St., Cotacachi, Ecuador', NULL, 1, SYSDATETIMEOFFSET(), '1.0', NULL, NULL),
	('a1000000-0000-0000-0000-000000000003', 'Accuracy User 3', 'accuracy3@amigoalert.test', '+593980000003', '3 Test St., Cotacachi, Ecuador', NULL, 1, SYSDATETIMEOFFSET(), '1.0', NULL, NULL),
	('a1000000-0000-0000-0000-000000000004', 'Accuracy User 4', 'accuracy4@amigoalert.test', '+593980000004', '4 Test St., Cotacachi, Ecuador', NULL, 1, SYSDATETIMEOFFSET(), '1.0', NULL, NULL),
	('a1000000-0000-0000-0000-000000000005', 'Accuracy User 5', 'accuracy5@amigoalert.test', '+593980000005', '5 Test St., Cotacachi, Ecuador', NULL, 1, SYSDATETIMEOFFSET(), '1.0', NULL, NULL),
	('a1000000-0000-0000-0000-000000000006', 'Accuracy User 6', 'accuracy6@amigoalert.test', '+593980000006', '6 Test St., Cotacachi, Ecuador', NULL, 1, SYSDATETIMEOFFSET(), '1.0', NULL, NULL);

-- ConsentStatusId 2 = Granted: the only state in which the contact may be messaged.
INSERT INTO [EmergencyContact] ([Id], [UserId], [Name], [Phone], [ConsentStatusId], [ConsentToken], [ConsentTokenExpires],
	[ConsentRequestedAt], [ConsentRespondedAt], [IsActive])
VALUES
	('c1000000-0000-0000-0000-000000000001', 'a1000000-0000-0000-0000-000000000001', 'Accuracy Contact 1', '+593990000001', 2, NULL, NULL, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 1),
	('c1000000-0000-0000-0000-000000000002', 'a1000000-0000-0000-0000-000000000002', 'Accuracy Contact 2', '+593990000002', 2, NULL, NULL, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 1),
	('c1000000-0000-0000-0000-000000000003', 'a1000000-0000-0000-0000-000000000003', 'Accuracy Contact 3', '+593990000003', 2, NULL, NULL, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 1),
	('c1000000-0000-0000-0000-000000000004', 'a1000000-0000-0000-0000-000000000004', 'Accuracy Contact 4', '+593990000004', 2, NULL, NULL, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 1),
	('c1000000-0000-0000-0000-000000000005', 'a1000000-0000-0000-0000-000000000005', 'Accuracy Contact 5', '+593990000005', 2, NULL, NULL, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 1),
	('c1000000-0000-0000-0000-000000000006', 'a1000000-0000-0000-0000-000000000006', 'Accuracy Contact 6', '+593990000006', 2, NULL, NULL, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), 1);
