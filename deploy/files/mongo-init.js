db = db.getSiblingDB('IngestionRefineryDb');
db.createUser({
  user: process.env.MONGO_APP_USERNAME,
  pwd: process.env.MONGO_APP_PASSWORD,
  roles: [{ role: 'readWrite', db: 'IngestionRefineryDb' }]
});
