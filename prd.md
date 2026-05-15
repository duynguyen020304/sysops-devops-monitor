# Product Requirements Document

# SysOps and DevOps Monitoring Platform

## 1. Product Overview

### 1.1 Product Name

SysOps and DevOps Monitoring Platform

### 1.2 Product Summary

The SysOps and DevOps Monitoring Platform is a centralized monitoring system designed to help engineering teams observe, analyze, and respond to operational events across software delivery pipelines and VPS infrastructure.

The system focuses on four main monitoring areas:

1. GitHub Actions CI/CD workflow logs
2. PM2 process logs on VPS servers
3. Server resource metrics including CPU, RAM, disk, and network usage
4. Alerting and operational visibility for developers, DevOps engineers, and system administrators

The goal is to provide a single dashboard where teams can understand the health of their deployment pipelines, application processes, and server infrastructure without manually checking GitHub, SSH terminals, PM2 commands, or separate monitoring tools.

---

## 2. Background and Problem Statement

Modern software systems are often deployed through automated CI/CD pipelines and hosted on VPS servers. While tools such as GitHub Actions, PM2, Linux system commands, and server dashboards provide useful information, the data is usually scattered across different places.

Developers may need to open GitHub Actions to inspect failed deployments. System administrators may need to SSH into the VPS to check PM2 logs, CPU usage, RAM pressure, disk space, or network traffic. This fragmented workflow slows down incident response and makes it difficult to understand the full operational picture.

Common problems include:

- CI/CD failures are not visible in the same place as runtime application errors.
- PM2 process logs require manual SSH access or command-line inspection.
- CPU, RAM, disk, and network metrics are not always tracked historically.
- Teams may notice server problems only after users report issues.
- There is no unified timeline connecting deployment events, process restarts, errors, and resource spikes.
- Junior developers may struggle to diagnose whether an issue comes from code, deployment, PM2, or the VPS itself.

This product aims to solve these problems by creating a unified monitoring interface for both DevOps and SysOps activities.

---

## 3. Product Goals

### 3.1 Business Goals

- Reduce the time required to detect and diagnose deployment and server issues.
- Improve operational visibility for small and medium engineering teams.
- Provide a centralized monitoring system for GitHub Actions, PM2, and VPS metrics.
- Help teams prevent downtime by detecting abnormal behavior early.
- Support better collaboration between developers, DevOps engineers, and system administrators.

### 3.2 User Goals

Users should be able to:

- View GitHub Actions workflow status and logs from a central dashboard.
- Track PM2 process status, restarts, errors, and logs for each VPS.
- Monitor CPU, RAM, disk, and network metrics in near real time.
- Receive alerts when deployments fail, PM2 processes crash, or server resources exceed thresholds.
- Search and filter logs by repository, workflow, server, process, severity, and time range.
- Correlate deployment events with runtime errors and infrastructure spikes.

---

## 4. Target Users

### 4.1 DevOps Engineer

The DevOps Engineer is responsible for CI/CD pipelines, deployments, environment stability, infrastructure automation, and incident response.

Main needs:

- Quickly detect failed GitHub Actions workflows.
- View deployment logs without switching to GitHub.
- Understand whether a failed deployment affected PM2 processes.
- Monitor resource usage after deployments.
- Configure alerts for CI/CD and server health.

### 4.2 System Administrator

The System Administrator is responsible for VPS health, Linux server monitoring, network usage, disk usage, and process stability.

Main needs:

- Monitor CPU, RAM, disk, and network metrics.
- Track PM2 process status and resource usage.
- Detect disk exhaustion, memory pressure, high CPU usage, and network anomalies.
- Review historical server behavior.
- Receive alerts before server issues become critical.

### 4.3 Backend Developer

The Backend Developer is responsible for application logic, APIs, background jobs, and runtime errors.

Main needs:

- View PM2 logs for specific application processes.
- Search application errors by time range or keyword.
- Check whether recent deployments introduced errors.
- See process crashes, restarts, and memory leaks.
- Understand the relationship between code deployments and runtime incidents.

### 4.4 Engineering Manager or Technical Lead

The Engineering Manager or Technical Lead needs a high-level view of operational reliability.

Main needs:

- View system health at a glance.
- Understand deployment stability trends.
- Review incident history.
- Track alert frequency and response patterns.
- Evaluate whether the engineering process is improving over time.

---

## 5. Scope

### 5.1 In Scope

The first version of the product will include:

- GitHub Actions workflow monitoring
- GitHub Actions log collection and display
- PM2 process discovery and monitoring
- PM2 process log collection
- VPS server metrics collection
- CPU usage monitoring
- RAM usage monitoring
- Disk usage monitoring
- Network traffic monitoring
- Dashboard for repositories, servers, processes, and alerts
- Searchable log viewer
- Basic alert rules
- User authentication and role-based access control
- Historical metrics storage
- Basic incident timeline view

### 5.2 Out of Scope for Version 1

The following features are not required in the first version:

- Full Kubernetes monitoring
- Cloud provider billing analysis
- Automatic rollback execution
- AI-based root cause analysis
- Distributed tracing
- Application performance monitoring at code level
- Multi-cloud infrastructure provisioning
- Advanced anomaly detection
- Full SIEM functionality
- Compliance reporting

These features may be considered in future versions.

---

## 6. Key Use Cases

### 6.1 Monitor GitHub Actions CI/CD Status

As a DevOps Engineer, I want to view GitHub Actions workflow runs in the monitoring dashboard so that I can quickly know whether builds, tests, and deployments are passing or failing.

Acceptance criteria:

- The system displays workflow runs by repository.
- Each workflow run shows status, conclusion, branch, commit SHA, actor, start time, end time, and duration.
- Failed workflow runs are visually highlighted.
- Users can open detailed logs for each job and step.
- Users can filter workflows by status, repository, branch, and time range.

### 6.2 Inspect GitHub Actions Logs

As a developer, I want to inspect CI/CD logs directly inside the platform so that I do not need to switch between different tools during debugging.

Acceptance criteria:

- Users can view logs by workflow run, job, and step.
- Logs preserve timestamp order.
- Users can search logs by keyword.
- Users can filter logs by error, warning, and general output.
- Long logs are paginated or streamed efficiently.

### 6.3 Monitor PM2 Processes on VPS

As a System Administrator, I want to monitor every PM2 process running on a VPS so that I can detect crashes, restarts, and unhealthy services.

Acceptance criteria:

- The system lists PM2 processes for each connected VPS.
- Each process displays name, process ID, status, uptime, restart count, CPU usage, memory usage, and execution mode.
- The system highlights stopped, errored, or frequently restarting processes.
- Users can view historical status changes for each process.
- Users can filter processes by server, application, status, or process name.

### 6.4 View PM2 Logs per Process

As a Backend Developer, I want to view logs for each PM2 process so that I can debug runtime errors and application behavior.

Acceptance criteria:

- Users can select a VPS and PM2 process to view logs.
- The system separates standard output logs and error logs.
- Users can search logs by keyword, severity, and time range.
- Logs include timestamps and source process information.
- The system supports near real-time log streaming.

### 6.5 Monitor VPS CPU, RAM, Disk, and Network Metrics

As a System Administrator, I want to monitor VPS resource usage so that I can detect performance problems before they affect users.

Acceptance criteria:

- The dashboard displays CPU usage, RAM usage, disk usage, disk I/O, and network traffic.
- Metrics are available at server level.
- Metrics are stored historically.
- Users can view charts by time range.
- The system supports threshold-based alerts.

### 6.6 Correlate Deployment Events with Runtime Problems

As a Technical Lead, I want to see deployment events together with PM2 and VPS metrics so that I can understand whether a deployment caused runtime issues.

Acceptance criteria:

- The system shows a timeline of GitHub Actions events, PM2 restarts, error spikes, and server resource spikes.
- Users can filter the timeline by server, repository, process, and time range.
- Deployment events are linked to commit SHA and workflow logs.
- PM2 incidents are linked to process logs.

### 6.7 Receive Alerts for Critical Events

As a DevOps Engineer, I want to receive alerts when CI/CD fails, PM2 crashes, or server resources are unhealthy so that I can respond quickly.

Acceptance criteria:

- Users can configure alert rules.
- Alerts can be triggered by GitHub Actions failure, PM2 process stopped, high restart count, high CPU, high RAM, high disk usage, or abnormal network traffic.
- Alerts include severity, affected resource, timestamp, and suggested inspection links.
- Alerts are shown in the dashboard.
- Alert history is stored.

---

## 7. Functional Requirements

## 7.1 GitHub Actions Monitoring

### 7.1.1 Repository Connection

The system shall allow users to connect GitHub repositories for monitoring.

Requirements:

- Support GitHub personal access token or GitHub App authentication.
- Allow users to select repositories to monitor.
- Store repository metadata including owner, name, default branch, and visibility.
- Support multiple repositories under one workspace.

### 7.1.2 Workflow Run Collection

The system shall collect GitHub Actions workflow run data.

Data to collect:

- Repository name
- Workflow name
- Workflow run ID
- Job ID
- Branch
- Commit SHA
- Commit message
- Triggering actor
- Event type
- Status
- Conclusion
- Start time
- End time
- Duration
- Workflow URL

### 7.1.3 Workflow Log Collection

The system shall collect and display logs from GitHub Actions workflow runs.

Requirements:

- Fetch logs for workflow jobs and steps.
- Store logs in a searchable format.
- Preserve log timestamps when available.
- Support partial log loading for large workflows.
- Mark error and warning lines when detectable.

### 7.1.4 Workflow Dashboard

The system shall provide a dashboard for GitHub Actions.

Dashboard elements:

- Total workflow runs
- Successful runs
- Failed runs
- Cancelled runs
- Average duration
- Failure rate
- Recent failed workflows
- Workflow status by repository
- Workflow status by branch

---

## 7.2 PM2 Process Monitoring

### 7.2.1 VPS Agent

The system shall use a lightweight agent installed on each VPS to collect PM2 and system metrics.

Agent responsibilities:

- Discover PM2 processes.
- Collect PM2 process metadata.
- Stream or periodically send PM2 logs.
- Collect server metrics.
- Send heartbeat signals to the central platform.
- Buffer data temporarily if the central platform is unavailable.

### 7.2.2 PM2 Process Discovery

The system shall automatically detect PM2 processes running on the VPS.

Data to collect:

- Server ID
- Process name
- PM2 ID
- Process ID
- Status
- Uptime
- Restart count
- CPU usage
- Memory usage
- Node.js version if available
- Execution mode
- Watch mode status
- Created time
- Last restart time

### 7.2.3 PM2 Log Collection

The system shall collect logs from each PM2 process.

Requirements:

- Collect standard output logs.
- Collect error logs.
- Associate each log line with the correct server and PM2 process.
- Add timestamp if the original log line does not include one.
- Support search by keyword, process, server, and time range.
- Support near real-time log streaming.

### 7.2.4 PM2 Health Rules

The system shall detect unhealthy PM2 behavior.

Examples:

- Process status is stopped or errored.
- Process restart count exceeds threshold within a time window.
- Process memory usage keeps increasing over time.
- Process CPU usage remains high for a configured period.
- No logs or heartbeat from process for a suspicious period.

---

## 7.3 VPS Infrastructure Monitoring

### 7.3.1 CPU Metrics

The system shall monitor CPU usage for each VPS.

Metrics:

- Total CPU usage percentage
- Per-core CPU usage if available
- Load average
- Process-level CPU usage for PM2 processes

### 7.3.2 RAM Metrics

The system shall monitor RAM usage for each VPS.

Metrics:

- Total memory
- Used memory
- Free memory
- Memory usage percentage
- Swap usage
- Process-level memory usage for PM2 processes

### 7.3.3 Disk Metrics

The system shall monitor disk usage and disk behavior.

Metrics:

- Total disk space
- Used disk space
- Free disk space
- Disk usage percentage
- Disk read rate
- Disk write rate
- Mount point usage

### 7.3.4 Network Metrics

The system shall monitor network-related metrics.

Metrics:

- Incoming traffic
- Outgoing traffic
- Packets received
- Packets sent
- Network errors if available
- Network drops if available
- Bandwidth usage over time

### 7.3.5 Server Health Status

The system shall calculate an overall health status for each VPS.

Possible statuses:

- Healthy
- Warning
- Critical
- Unknown

Health status should consider:

- Agent heartbeat
- CPU usage
- RAM usage
- Disk usage
- Network condition
- PM2 process health
- Recent critical alerts

---

## 7.4 Dashboard Requirements

### 7.4.1 Main Overview Dashboard

The main dashboard shall provide a high-level operational view.

Sections:

- Overall system health
- Connected repositories
- Connected VPS servers
- Active PM2 processes
- Recent GitHub Actions failures
- Recent PM2 errors
- CPU, RAM, disk, and network summary
- Active alerts
- Incident timeline

### 7.4.2 Repository Detail Page

The repository detail page shall show CI/CD activity for one repository.

Sections:

- Workflow run list
- Workflow status trend
- Failure rate
- Average workflow duration
- Recent failed jobs
- Branch filter
- Commit and actor information
- Log viewer

### 7.4.3 VPS Detail Page

The VPS detail page shall show infrastructure and process data for one server.

Sections:

- Server status
- Agent heartbeat
- CPU chart
- RAM chart
- Disk chart
- Network chart
- PM2 process table
- Recent alerts
- Recent logs

### 7.4.4 PM2 Process Detail Page

The PM2 process detail page shall focus on one application process.

Sections:

- Process status
- Uptime
- Restart count
- CPU chart
- Memory chart
- Standard output logs
- Error logs
- Recent restarts
- Related deployment events

### 7.4.5 Alert Center

The alert center shall show all active and historical alerts.

Sections:

- Active alerts
- Resolved alerts
- Alert severity
- Affected resource
- Triggered rule
- Timestamp
- Alert owner
- Resolution note

---

## 8. Log Management Requirements

### 8.1 Log Sources

The system shall support the following log sources in version 1:

- GitHub Actions workflow logs
- PM2 standard output logs
- PM2 error logs
- VPS agent operational logs

### 8.2 Log Search

Users shall be able to search logs by:

- Keyword
- Time range
- Repository
- Workflow
- Workflow run
- Server
- PM2 process
- Severity
- Source type

### 8.3 Log Filtering

Users shall be able to filter logs by:

- Error lines
- Warning lines
- Info lines
- Deployment logs
- Runtime logs
- Process-specific logs

### 8.4 Log Retention

The system shall support configurable log retention.

Default retention proposal:

- GitHub Actions logs: 30 days
- PM2 logs: 14 days
- Error logs: 60 days
- Alert history: 180 days
- Aggregated metrics: 365 days

### 8.5 Log Privacy

The system shall protect sensitive log data.

Requirements:

- Mask common secrets such as tokens, passwords, API keys, and private keys when detectable.
- Restrict log access by user role.
- Avoid exposing raw environment variables unless explicitly allowed.
- Record audit logs for sensitive access.

---

## 9. Alerting Requirements

### 9.1 Alert Types

The system shall support alerts for:

- GitHub Actions workflow failure
- GitHub Actions deployment failure
- PM2 process stopped
- PM2 process errored
- PM2 restart count exceeds threshold
- High CPU usage
- High RAM usage
- High disk usage
- Abnormal network traffic
- VPS agent offline
- Log error spike

### 9.2 Alert Severity

Alert severity levels:

- Info
- Warning
- Critical

Examples:

- Info: Deployment completed successfully.
- Warning: CPU usage above 75% for 10 minutes.
- Critical: PM2 process stopped or disk usage above 90%.

### 9.3 Alert Rule Configuration

Users shall be able to configure:

- Metric threshold
- Time window
- Severity
- Target resource
- Notification channel
- Cooldown period
- Auto-resolve condition

### 9.4 Notification Channels

Version 1 should support dashboard notifications. Future versions may support:

- Email
- Slack
- Discord
- Telegram
- Webhook
- SMS

### 9.5 Alert Lifecycle

Alert states:

- Triggered
- Acknowledged
- Resolved
- Muted

Each alert shall include:

- Alert ID
- Title
- Description
- Severity
- Source
- Affected resource
- Trigger time
- Resolution time if resolved
- Related logs
- Related metrics

---

## 10. User Roles and Permissions

### 10.1 Roles

The system shall support role-based access control.

Default roles:

1. Owner
2. Admin
3. DevOps Engineer
4. Developer
5. Viewer

### 10.2 Permission Matrix

| Feature | Owner | Admin | DevOps Engineer | Developer | Viewer |
|---|---:|---:|---:|---:|---:|
| Manage workspace | Yes | No | No | No | No |
| Manage users | Yes | Yes | No | No | No |
| Connect GitHub repositories | Yes | Yes | Yes | No | No |
| Connect VPS agents | Yes | Yes | Yes | No | No |
| View dashboards | Yes | Yes | Yes | Yes | Yes |
| View GitHub Actions logs | Yes | Yes | Yes | Yes | Yes |
| View PM2 logs | Yes | Yes | Yes | Yes | Limited |
| Configure alerts | Yes | Yes | Yes | No | No |
| Acknowledge alerts | Yes | Yes | Yes | Yes | No |
| Resolve alerts | Yes | Yes | Yes | No | No |
| Manage retention settings | Yes | Yes | No | No | No |

---

## 11. Data Model

### 11.1 Workspace

Represents an organization or team.

Fields:

- workspace_id
- name
- owner_user_id
- created_at
- updated_at

### 11.2 User

Represents a platform user.

Fields:

- user_id
- workspace_id
- name
- email
- role
- status
- created_at
- updated_at

### 11.3 Repository

Represents a connected GitHub repository.

Fields:

- repository_id
- workspace_id
- provider
- owner
- name
- full_name
- default_branch
- visibility
- github_repository_id
- created_at
- updated_at

### 11.4 Workflow Run

Represents a GitHub Actions workflow run.

Fields:

- workflow_run_id
- repository_id
- workflow_name
- github_run_id
- branch
- commit_sha
- commit_message
- actor
- event_type
- status
- conclusion
- started_at
- completed_at
- duration_seconds
- html_url

### 11.5 Workflow Log

Represents a GitHub Actions log line or log block.

Fields:

- log_id
- workflow_run_id
- job_name
- step_name
- timestamp
- level
- message
- raw_message
- created_at

### 11.6 Server

Represents a connected VPS.

Fields:

- server_id
- workspace_id
- hostname
- ip_address
- operating_system
- agent_version
- status
- last_heartbeat_at
- created_at
- updated_at

### 11.7 PM2 Process

Represents a PM2 process running on a VPS.

Fields:

- process_id
- server_id
- pm2_id
- name
- pid
- status
- uptime_seconds
- restart_count
- cpu_usage
- memory_usage
- execution_mode
- node_version
- created_at
- updated_at

### 11.8 PM2 Log

Represents a PM2 log line.

Fields:

- log_id
- server_id
- process_id
- stream_type
- timestamp
- level
- message
- raw_message
- created_at

### 11.9 Server Metric

Represents server resource metrics.

Fields:

- metric_id
- server_id
- timestamp
- cpu_usage_percent
- memory_total_bytes
- memory_used_bytes
- memory_usage_percent
- swap_used_bytes
- disk_total_bytes
- disk_used_bytes
- disk_usage_percent
- disk_read_bytes_per_second
- disk_write_bytes_per_second
- network_rx_bytes_per_second
- network_tx_bytes_per_second
- load_average_1m
- load_average_5m
- load_average_15m

### 11.10 Alert

Represents an alert event.

Fields:

- alert_id
- workspace_id
- source_type
- source_id
- title
- description
- severity
- status
- triggered_at
- acknowledged_at
- resolved_at
- assigned_user_id
- rule_id

### 11.11 Alert Rule

Represents a configured alert rule.

Fields:

- rule_id
- workspace_id
- name
- source_type
- condition_type
- threshold
- time_window_seconds
- severity
- is_enabled
- cooldown_seconds
- created_at
- updated_at

---

## 12. System Architecture

### 12.1 High-Level Architecture

The system should include the following components:

1. Web Dashboard
2. Backend API
3. GitHub Integration Service
4. VPS Agent
5. Metrics Collector
6. Log Ingestion Service
7. Alerting Engine
8. Database
9. Time-Series Storage
10. Log Storage

### 12.2 Web Dashboard

Responsibilities:

- Display operational dashboards.
- Provide log search interface.
- Show metric charts.
- Manage alerts and rules.
- Manage repositories and servers.
- Manage users and permissions.

### 12.3 Backend API

Responsibilities:

- Authenticate users.
- Serve dashboard data.
- Manage workspace resources.
- Receive data from VPS agents.
- Provide search and filtering APIs.
- Expose alert management APIs.

### 12.4 GitHub Integration Service

Responsibilities:

- Connect to GitHub API.
- Fetch workflow runs.
- Fetch workflow logs.
- Normalize GitHub Actions data.
- Store workflow status and logs.
- Handle rate limits carefully.

### 12.5 VPS Agent

Responsibilities:

- Run on each VPS.
- Collect PM2 process data.
- Collect PM2 logs.
- Collect CPU, RAM, disk, and network metrics.
- Send heartbeat to backend.
- Retry sending data when network is unstable.
- Use minimal CPU and RAM.

### 12.6 Log Ingestion Service

Responsibilities:

- Receive logs from GitHub and VPS agents.
- Normalize log format.
- Detect severity when possible.
- Mask sensitive data.
- Store logs for search and retrieval.

### 12.7 Alerting Engine

Responsibilities:

- Evaluate alert rules.
- Trigger alerts when conditions are met.
- Avoid duplicate alerts using cooldown rules.
- Auto-resolve alerts when conditions return to normal.
- Store alert history.

---

## 13. API Requirements

### 13.1 Authentication APIs

- Register user
- Login user
- Logout user
- Refresh token
- Get current user

### 13.2 Repository APIs

- List repositories
- Connect repository
- Disconnect repository
- Get repository details
- List workflow runs
- Get workflow run details
- Get workflow logs

### 13.3 Server APIs

- Register VPS agent
- List servers
- Get server details
- Update server metadata
- Delete server
- Get server metrics
- Get server health

### 13.4 PM2 APIs

- List PM2 processes by server
- Get PM2 process details
- Get PM2 process logs
- Get PM2 process metrics
- Get PM2 process restart history

### 13.5 Log APIs

- Search logs
- Filter logs
- Stream logs
- Export logs

### 13.6 Alert APIs

- List alerts
- Get alert details
- Create alert rule
- Update alert rule
- Delete alert rule
- Acknowledge alert
- Resolve alert
- Mute alert

---

## 14. Non-Functional Requirements

### 14.1 Performance

- Dashboard overview should load within 3 seconds under normal conditions.
- Log search should return initial results within 5 seconds for common queries.
- Metrics should be ingested at configurable intervals, with a default interval of 10 to 30 seconds.
- PM2 log streaming should have low latency, ideally under 5 seconds.

### 14.2 Scalability

Version 1 should support:

- 1 workspace with multiple users
- At least 20 repositories per workspace
- At least 20 VPS servers per workspace
- At least 200 PM2 processes per workspace
- At least 1 million log lines per workspace per month

The architecture should allow future scaling through queue-based ingestion and separate storage for logs and metrics.

### 14.3 Reliability

- The VPS agent should continue running after server reboot.
- The agent should buffer data temporarily if the backend is unreachable.
- The backend should handle duplicate data safely.
- Alert evaluation should be reliable and avoid excessive false positives.

### 14.4 Security

- All communication between agent and backend must use HTTPS.
- Agent authentication should use secure tokens.
- GitHub tokens must be encrypted at rest.
- User passwords must be hashed securely.
- Role-based access control must be enforced on all APIs.
- Sensitive logs should be masked when possible.
- Audit logs should be stored for administrative actions.

### 14.5 Maintainability

- The system should be modular.
- GitHub integration, PM2 monitoring, metric collection, log ingestion, and alerting should be separated logically.
- The VPS agent should have clear versioning.
- Configuration should be environment-based.
- APIs should be documented.

### 14.6 Usability

- The dashboard should prioritize clarity over visual complexity.
- Critical information should be visible without deep navigation.
- Users should be able to find logs quickly.
- Alerts should include direct links to related logs and metrics.
- Charts should provide useful time ranges such as 15 minutes, 1 hour, 6 hours, 24 hours, 7 days, and 30 days.

---

## 15. Monitoring Metrics

### 15.1 CI/CD Metrics

- Total workflow runs
- Success rate
- Failure rate
- Average workflow duration
- Failed workflow count
- Cancelled workflow count
- Deployment frequency
- Most frequently failing workflow
- Most frequently failing branch

### 15.2 PM2 Metrics

- Total PM2 processes
- Running processes
- Stopped processes
- Errored processes
- Restart count per process
- CPU usage per process
- Memory usage per process
- Process uptime
- Error log frequency

### 15.3 VPS Metrics

- CPU usage
- RAM usage
- Swap usage
- Disk usage
- Disk read and write rate
- Network receive rate
- Network transmit rate
- Load average
- Agent heartbeat status

### 15.4 Alert Metrics

- Active alerts
- Alerts by severity
- Alerts by source
- Mean time to acknowledge
- Mean time to resolve
- Repeated alerts
- Noisy alert rules

---

## 16. User Experience Requirements

### 16.1 Main Navigation

Recommended navigation structure:

- Overview
- GitHub Actions
- Servers
- PM2 Processes
- Logs
- Alerts
- Settings

### 16.2 Overview Page

The overview page should answer these questions immediately:

- Are all servers healthy?
- Are all PM2 processes running?
- Did any GitHub Actions workflow fail recently?
- Are CPU, RAM, disk, or network usage abnormal?
- Are there active critical alerts?

### 16.3 Log Viewer Experience

The log viewer should include:

- Search box
- Time range selector
- Source filter
- Severity filter
- Auto-scroll option
- Pause live stream option
- Copy log line option
- Link to related workflow, server, or process

### 16.4 Metrics Chart Experience

Charts should include:

- Clear metric labels
- Time range selector
- Hover details
- Alert threshold lines
- Deployment event markers
- PM2 restart markers

### 16.5 Empty States

The system should provide helpful empty states.

Examples:

- No repository connected yet.
- No VPS agent installed yet.
- No logs found for this filter.
- No alerts triggered in this time range.

Each empty state should explain the next action clearly.

---

## 17. MVP Definition

The minimum viable product should include the following:

### 17.1 Must Have

- User authentication
- Workspace setup
- Connect GitHub repository
- Fetch GitHub Actions workflow runs
- Display workflow status and logs
- Install and register VPS agent
- Detect PM2 processes
- Display PM2 process status
- Display PM2 logs by process
- Collect CPU, RAM, disk, and network metrics
- Display server metrics charts
- Basic alert rules
- Alert center
- Search logs by keyword and time range

### 17.2 Should Have

- Real-time PM2 log streaming
- Deployment and incident timeline
- Alert acknowledgement and resolution
- Log masking for sensitive values
- Role-based access control
- Historical charts

### 17.3 Could Have

- Email notifications
- Slack or Discord notifications
- Log export
- Custom dashboard widgets
- Agent auto-update
- Advanced anomaly detection

### 17.4 Will Not Have in MVP

- Kubernetes monitoring
- Cloud billing monitoring
- Automatic remediation
- AI root cause analysis
- Full distributed tracing

---

## 18. Milestones

### Milestone 1: Foundation

Deliverables:

- Authentication
- Workspace management
- Basic dashboard layout
- Repository model
- Server model
- Initial database schema

### Milestone 2: GitHub Actions Monitoring

Deliverables:

- GitHub repository connection
- Workflow run synchronization
- Workflow status dashboard
- Workflow log viewer
- Basic CI/CD filters

### Milestone 3: VPS Agent and PM2 Monitoring

Deliverables:

- VPS agent registration
- PM2 process discovery
- PM2 process dashboard
- PM2 log collection
- PM2 log viewer

### Milestone 4: Server Metrics Monitoring

Deliverables:

- CPU metrics
- RAM metrics
- Disk metrics
- Network metrics
- Metrics charts
- Server health status

### Milestone 5: Alerting and Incident Timeline

Deliverables:

- Alert rule configuration
- Alert triggering
- Alert center
- Alert lifecycle management
- Deployment and runtime event timeline

### Milestone 6: Security, Reliability, and Release Preparation

Deliverables:

- Role-based access control
- Token encryption
- Sensitive log masking
- Agent retry and buffering
- Performance testing
- Documentation

---

## 19. Success Metrics

### 19.1 Product Success Metrics

- Users can connect a GitHub repository within 5 minutes.
- Users can install and register a VPS agent within 10 minutes.
- Users can find a failed GitHub Actions log within 30 seconds.
- Users can find PM2 error logs for a specific process within 30 seconds.
- Users can identify high CPU, RAM, disk, or network usage from the dashboard within 10 seconds.

### 19.2 Operational Success Metrics

- Workflow failure detection latency below 2 minutes.
- PM2 process crash detection latency below 1 minute.
- Server metric ingestion delay below 30 seconds under normal conditions.
- Alert false positive rate remains manageable and reviewed regularly.
- Dashboard availability remains above 99% for internal use.

### 19.3 User Satisfaction Metrics

- Users report reduced need to SSH into VPS for basic diagnosis.
- Users report easier debugging of deployment failures.
- Users report faster understanding of whether incidents are caused by deployment, application, or infrastructure issues.

---

## 20. Risks and Mitigations

### 20.1 GitHub API Rate Limits

Risk:

The system may hit GitHub API rate limits when monitoring many repositories or frequently fetching logs.

Mitigation:

- Use efficient polling intervals.
- Cache workflow data.
- Fetch logs only when needed or when workflows fail.
- Prefer webhook-based updates where possible.

### 20.2 Large Log Volume

Risk:

PM2 and CI/CD logs may grow quickly and increase storage cost.

Mitigation:

- Implement retention policies.
- Compress logs.
- Store error logs longer than standard logs.
- Allow users to configure retention.

### 20.3 Agent Security

Risk:

The VPS agent may expose sensitive server information if not secured properly.

Mitigation:

- Use secure agent tokens.
- Use HTTPS only.
- Limit agent permissions.
- Rotate tokens.
- Avoid collecting unnecessary sensitive data.

### 20.4 False Positive Alerts

Risk:

Too many alerts may cause alert fatigue.

Mitigation:

- Use cooldown periods.
- Require sustained threshold violations before triggering alerts.
- Support alert severity levels.
- Allow users to mute noisy rules.

### 20.5 Incomplete PM2 Data

Risk:

PM2 process information may be incomplete if PM2 is not installed correctly or runs under a different user.

Mitigation:

- Provide installation diagnostics.
- Show agent permission warnings.
- Document PM2 setup requirements clearly.

---

## 21. Open Questions

1. Should the system use GitHub webhooks, polling, or both for workflow updates?
2. Should PM2 logs be streamed continuously or collected at intervals?
3. What is the expected number of VPS servers in the first production use case?
4. What is the expected log volume per server per day?
5. Which notification channels should be prioritized after dashboard alerts?
6. Should users be allowed to execute PM2 actions from the dashboard, such as restart or stop process?
7. Should the agent support Docker container metrics in the future?
8. Should the system support multiple environments such as development, staging, and production?
9. What default alert thresholds should be used for CPU, RAM, disk, and network metrics?
10. How long should logs and metrics be retained by default?

---

## 22. Recommended Default Alert Rules

### 22.1 GitHub Actions

| Alert | Condition | Severity |
|---|---|---|
| Workflow failed | Workflow conclusion is failure | Warning |
| Deployment failed | Deployment workflow conclusion is failure | Critical |
| Workflow duration too long | Duration exceeds historical average by configured threshold | Warning |

### 22.2 PM2

| Alert | Condition | Severity |
|---|---|---|
| Process stopped | PM2 status is stopped | Critical |
| Process errored | PM2 status is errored | Critical |
| High restart count | More than 3 restarts in 10 minutes | Warning |
| High memory usage | Process memory exceeds configured threshold | Warning |
| High CPU usage | Process CPU usage above 80% for 5 minutes | Warning |

### 22.3 VPS

| Alert | Condition | Severity |
|---|---|---|
| High CPU usage | CPU above 85% for 10 minutes | Warning |
| Critical CPU usage | CPU above 95% for 5 minutes | Critical |
| High RAM usage | RAM above 85% for 10 minutes | Warning |
| Critical RAM usage | RAM above 95% for 5 minutes | Critical |
| High disk usage | Disk above 80% | Warning |
| Critical disk usage | Disk above 90% | Critical |
| Agent offline | No heartbeat for 2 minutes | Critical |

---