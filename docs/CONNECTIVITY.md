# Connectivity Guide

This guide explains how InControl-Desktop handles network connectivity and how you maintain control.

## Core Principle

> **Nothing connects without your explicit approval.**

InControl-Desktop is designed to work completely offline. When you do enable connectivity, you have full visibility and control over all network activity.

---

## Connectivity Modes

### Offline Only (Default)

```
Status: Offline — no network activity
```

- **No network requests** of any kind
- All features work locally
- Complete privacy guarantee
- Cannot be changed by the assistant

This is the safest mode if you never need internet features.

### Assisted

```
Status: Online — approved operations only
```

- Network available for **approved endpoints only**
- Each new endpoint requires your permission
- Activity logged for review
- Good balance of functionality and control

Use this when you want specific online features but want approval for each new service.

### Connected

```
Status: Online — full access with audit
```

- Full network access for approved operations
- All activity logged in audit trail
- You can review what was accessed
- Best for productivity with transparency

Use this when you trust the assistant but want visibility.

---

## Changing Modes

### From Settings

1. Open Settings → Connectivity
2. Select your preferred mode
3. Changes take effect immediately

### Quick Offline (Panic Button)

If you ever need to go offline immediately:

1. Click the status indicator in the title bar
2. Click "Go Offline Now"
3. All network activity stops instantly

---

## Permission System

### How Permissions Work

When the assistant needs to access a new endpoint:

1. The request is blocked
2. You see a permission prompt
3. You choose: Allow, Ask Each Time, or Deny
4. Your choice is remembered

### Managing Permissions

View and edit all permissions in Settings → Connectivity → Permissions:

| Endpoint | Permission | Notes |
|----------|------------|-------|
| `https://api.github.com` | Always Allow | GitHub API |
| `https://api.weather.com` | Ask Each Time | Weather queries |
| `https://tracking.example.com` | Deny | Blocked |

### Permission Levels

- **Always Allow**: Requests proceed automatically
- **Ask Each Time**: Prompt before each request
- **Deny**: All requests blocked silently

---

## Audit Trail

### What's Logged

Every network request includes:

- **Timestamp**: When the request was made
- **Endpoint**: The URL accessed
- **Method**: GET, POST, etc.
- **Intent**: Why the request was needed
- **Result**: Success, failure, or blocked
- **Duration**: How long it took

### Viewing the Audit Log

Settings → Connectivity → Activity shows recent requests:

```
12:34:56 | GET https://api.weather.com/current | Success (152ms)
         | Intent: Get current weather for Seattle

12:35:02 | POST https://api.github.com/issues | Success (487ms)
         | Intent: Create issue for bug report
```

### Exporting the Log

Click "Export" to save the full audit log as JSON for external review.

---

## Internet Tool Requirements

When the assistant needs internet access, it must declare:

1. **Endpoint**: What URL it wants to access
2. **Method**: GET (read) or POST (send data)
3. **Purpose**: Why this request is needed
4. **Expected Data**: What information will be received
5. **Retention**: How long data will be kept

Example tool declaration:

```
Tool: weather.current
Endpoint: https://api.weather.com/current
Purpose: Fetch current weather conditions
Expected: Temperature, conditions, humidity
Retention: Until session ends
```

---

## Data Transparency

### What Gets Sent

In Assisted/Connected mode, the assistant may send:

- Query parameters (search terms, location names)
- API authentication (if you provide keys)
- Request metadata (timestamps, formats)

### What Doesn't Get Sent

- Your conversations (unless you explicitly request)
- Personal files or documents
- System information
- Other application data

### Reviewing Data Flow

The audit log shows `Data Sent` for each request so you can verify exactly what was transmitted.

---

## Revoking Access

### Removing a Permission

1. Settings → Connectivity → Permissions
2. Find the endpoint
3. Click "Remove" or change to "Deny"

### Clearing All Permissions

Settings → Connectivity → "Clear All Rules" removes all permissions. You'll be prompted again for each endpoint.

### Going Offline After Using Connectivity

You can always switch back to Offline Only mode. Previous permissions are preserved but inactive.

---

## Troubleshooting

### "Request Blocked" When I Approved It

Check that:
1. You approved the correct endpoint (paths matter)
2. You're in Assisted or Connected mode
3. The permission wasn't set to "Ask Each Time"

### Activity Not Showing in Audit Log

The audit log only shows:
- Requests that were attempted
- In Assisted or Connected mode

Offline Only mode generates no activity.

### Want to Test Connectivity

1. Switch to Connected mode
2. Ask the assistant to fetch something harmless (e.g., "What time is it in Tokyo?")
3. Review the activity log
4. Switch back to Offline if desired

---

## Best Practices

1. **Start Offline**: Only enable connectivity when you need it
2. **Use Assisted Mode**: Get approval prompts for new services
3. **Review Periodically**: Check the audit log occasionally
4. **Deny Unknown Endpoints**: When in doubt, deny
5. **Export Logs**: Save audit trails for important sessions

---

## Technical Details

### Network Gateway Architecture

All network requests go through a single gateway:

```
Assistant → InternetTool → ConnectivityManager → NetworkGateway → Internet
              ↓                    ↓
         Permission           Audit Log
           Check
```

### Request Flow

1. Assistant proposes a request with full declaration
2. InternetTool checks permissions
3. ConnectivityManager validates mode
4. NetworkGateway executes (if allowed)
5. Response logged in audit trail
6. Result returned to assistant

### No Bypass

The assistant cannot:
- Make direct HTTP requests
- Access network interfaces
- Modify connectivity settings
- Clear audit logs
- Skip permission checks

---

## Questions?

See [SUPPORT.md](../SUPPORT.md) for additional help.
