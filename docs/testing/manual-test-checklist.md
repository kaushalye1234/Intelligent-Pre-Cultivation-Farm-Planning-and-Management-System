# Manual Test Checklist

- Start the API and confirm `/health` is healthy.
- Log into React with a seeded admin or staff account.
- Confirm dashboard metrics render.
- Create a farm, field, and preliminary crop plan request.
- Create an inspection, observation, and high severity crop issue.
- Confirm invalid image upload is rejected and real upload succeeds only when Cloudinary is configured.
- Create resource category/resource/stock and reserve stock.
- Create and approve a farm task and irrigation schedule.
- Run the Flutter app with `AGRIASSIST_API_BASE_URL` pointed at the API and confirm login, dashboard refresh, crop plan form, inspection camera/GPS actions, and reservation form.