-- Get all artworks with their details
SELECT Id, AddedDate, Title, Description, CreatedDate, Category, Medium, Type, OriginalPath, R2Key, ThumbR2Key, VideoProvider, VideoId, Tags, `Groups`
FROM Artworks

WHERE Title LIKE '%Six legged war%';

-- Get record by title
SELECT Id, AddedDate, Title, Description, CreatedDate, Category, Medium, Type, OriginalPath, R2Key, ThumbR2Key, VideoProvider, VideoId, Tags, `Groups`
FROM Artworks
WHERE Title LIKE '%Skecth of flying organic spaceship%';

-- Select a specific artwork by its Id
SELECT Id, AddedDate, Title, Description, CreatedDate, Category, Medium, Type, OriginalPath, R2Key, ThumbR2Key, VideoProvider, VideoId, Tags, `Groups`
FROM Artworks   
WHERE Id = 34;


-- list Classifiers
SELECT * FROM Classifiers;

