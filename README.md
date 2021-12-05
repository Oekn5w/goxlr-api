# GoXLR API (unofficial)

## Overview GoXLR API

|API | Description | Request body | Response body |
|--- | ---- | ---- | ---- |
|`GET /` | API test | None | API online!|
|`GET /status` | Get status of the GoXLR connection | None | Status of the GoXLR connection|
|`GET /profilenames` | Get profile names | None | Array of the profile names|
|`POST /profile/set` | Set a new profile | New Profile Name | None|
|`POST /routing` | Edit routing entry | Action, In- and Output of the affected entry | None|
|`POST /routing/set` | Set routing entry | In- and Output of the affected entry | None|
|`POST /routing/clear` | Clear routing entry | In- and Output of the affected entry | None|
|`POST /routing/toggle` | Toggle routing entry | In- and Output of the affected entry | None|
