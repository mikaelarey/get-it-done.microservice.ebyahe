-- Source region.sql; codes 1-17; 17 rows.
SET IDENTITY_INSERT [CodeDecodeRegions] ON;
INSERT INTO [CodeDecodeRegions] ([Id], [Code], [Description], [Decode]) VALUES
('a0010001-0000-4000-8000-000000000001', 1, N'National Capital Region', N'NCR'),
('a0010001-0000-4000-8000-000000000002', 2, N'Cordillera Administrative Region', N'CAR'),
('a0010001-0000-4000-8000-000000000003', 3, N'Ilocos Region', N'Region I'),
('a0010001-0000-4000-8000-000000000004', 4, N'Cagayan Valley', N'Region II'),
('a0010001-0000-4000-8000-000000000005', 5, N'Central Luzon', N'Region III'),
('a0010001-0000-4000-8000-000000000006', 6, N'CALABARZON', N'Region IV-A'),
('a0010001-0000-4000-8000-000000000007', 7, N'MIMAROPA', N'Region IV-B'),
('a0010001-0000-4000-8000-000000000008', 8, N'Bicol Region', N'Region V'),
('a0010001-0000-4000-8000-000000000009', 9, N'Western Visayas', N'Region VI'),
('a0010001-0000-4000-8000-000000000010', 10, N'Central Visayas', N'Region VII'),
('a0010001-0000-4000-8000-000000000011', 11, N'Eastern Visayas', N'Region VIII'),
('a0010001-0000-4000-8000-000000000012', 12, N'Zamboanga Peninsula', N'Region IX'),
('a0010001-0000-4000-8000-000000000013', 13, N'Northern Mindanao', N'Region X'),
('a0010001-0000-4000-8000-000000000014', 14, N'Davao Region', N'Region XI'),
('a0010001-0000-4000-8000-000000000015', 15, N'SOCCSKSARGEN', N'Region XII'),
('a0010001-0000-4000-8000-000000000016', 16, N'CARAGA', N'Region XIII'),
('a0010001-0000-4000-8000-000000000017', 17, N'Autonomous Region in Muslim Mindanao', N'ARMM');
SET IDENTITY_INSERT [CodeDecodeRegions] OFF;
