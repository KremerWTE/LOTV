# Master Services Agreement
## MSA[####]: Lily of the Valley (LOTV) — [CLIENT ORGANIZATION NAME]

**FOR INTERNAL PURPOSES ONLY**

---

This Technology Consulting and Software Development Agreement (the "Agreement") is made by and between **WTE Solutions, DBA of PointShop, Inc.** ("WTE"), of 169 Boone Square Street, #230, Hillsborough, North Carolina 27278, a North Carolina Corporation, and **[CLIENT ORGANIZATION LEGAL NAME]** ("Client"), of [Client Address], [City, State, ZIP], with an effective date of [EFFECTIVE DATE].

---

## Services Overview

This Master Services Agreement ("Agreement") governs the provision of technology and software development services for the design, development, and deployment of **Lily of the Valley (LOTV)**, a .NET 9 SaaS social services coordination platform, and any other services selected from the Technology Services Catalog. See **Attachment A** for the Technology Services Catalog.

**Platform Overview:** LOTV is a cloud-hosted SaaS platform connecting people in need with donors, local volunteers, and coordinating staff. The platform includes service request management, donor contribution tracking (monetary and resources), volunteer coordination, fundraising event management, a donation tracking dashboard (by person, diocese, city, channel, and amount), an impact and distribution dashboard, and staff task management tools.

**Lead Technical Resource:** [WTE Lead Developer / CTO Name] shall serve as the primary technical lead on the LOTV engagement, responsible for architecture, software delivery, and technical quality assurance.

Specific services, deliverables, timelines, and fees shall be detailed in individual Statements of Work ("SOWs") or Estimates of Work ("EOWs") executed under this Agreement.

---

## Term and Termination

**Term:** This Agreement commences on the Effective Date and continues for a period of one (1) year, or until completion of all active SOWs, whichever is later. All Agreements are automatically renewed for another twelve (12) month term unless a written notice of cancellation is submitted in written form at least sixty (60) days in advance of the Agreement anniversary date.

**Termination for Cause:** Either party may terminate immediately upon material breach if not cured within fifteen (15) days of written notice. In the event that the Client fails to pay for such services per the terms of an active SOW/EOW, WTE may unilaterally suspend work on the LOTV project until payment is made, with applicable service charges applied to the past-due balance. Returned payments for insufficient funds will incur a service charge pursuant to WTE's current Payment Policy.

**Effect of Termination:** Client remains liable for all fees incurred through the termination date, including work in progress on any active milestone. All deliverables completed and paid for through the date of termination will be delivered to Client. Both parties must return or destroy confidential information within thirty (30) days. Provisions related to intellectual property, confidentiality, limitation of liability, and indemnification survive termination.

**Survival Provisions:** The following provisions shall survive termination or expiration of this Agreement:

- **Intellectual Property:** (a) WTE's ownership of pre-existing intellectual property, proprietary methodologies, frameworks, reusable component libraries, and general know-how; (b) Client's ownership of all LOTV custom application code, data models, and configurations created specifically under this Agreement; (c) License grants for WTE's retained intellectual property embedded in Client deliverables; (d) All intellectual property representations, warranties, and indemnification obligations of both parties.
- **Limitation of Liability:** All limitation of liability provisions, including damage caps and excluded damages categories; indemnification obligations and procedures; force majeure provisions.
- **Duration of Survival:** Unless otherwise specified, surviving provisions shall remain enforceable for a minimum of three (3) years from the date of Agreement termination.

---

## Fees and Payment Terms

**Payment Structure:**

| Service Type | Billing Method |
|---|---|
| Software Development (LOTV Platform) | Fixed fee per SOW milestone or time & materials at rates per Attachment C: Rate Card |
| Architecture & Design Services | Hourly at rates per Attachment C: Rate Card |
| Hosting & Cloud Infrastructure | Monthly recurring charges per services provisioned |
| Consulting & Advisory Services | Hourly at rates per Attachment C: Rate Card |
| Support & Maintenance (post-launch) | Monthly retainer or per-incident per applicable SOW |

**Milestone Billing:** For fixed-fee SOWs, WTE will invoice upon completion and Client acceptance of each defined milestone. Client has five (5) business days following WTE's written notice of milestone completion to either accept the milestone or provide written feedback identifying specific deficiencies. Failure to respond within five (5) business days constitutes acceptance.

**Payment Terms:**
- Invoices are due within thirty (30) days of receipt for Clients with approved credit; otherwise, due upon receipt.
- ACH payment is preferred. Remittance advices should be emailed to billing@wte.net.
- Late payments are subject to a 1.5% monthly service charge.
- Disputed amounts must be reported to WTE in writing within ten (10) days of invoice delivery. Undisputed portions remain due on the original due date.
- Services may be suspended for accounts sixty (60) or more days past due with five (5) days written notice.

**Expenses:** Client reimburses pre-approved out-of-pocket expenses including third-party software licenses, cloud infrastructure costs (Azure, AWS, or other), payment processor setup fees, and necessary travel expenses outside the Raleigh/Durham, North Carolina area. All reimbursable expenses exceeding $250 require prior written Client approval.

---

## Intellectual Property Rights

**WTE Intellectual Property:** WTE retains all rights to pre-existing and independently developed methodologies, frameworks, reusable code libraries, templates, scripts, monitoring solutions, development tooling, and non-client-specific innovations used in service delivery of the LOTV platform.

**Client-Specific Ownership (LOTV):** Upon final payment of all fees under the applicable SOW:

- **Custom Application Code:** Client receives full ownership of all LOTV application code developed exclusively for Client under this Agreement, including Lotv.Api, Lotv.Web, Lotv.Core, and Lotv.Tests projects.
- **Data:** Client retains full ownership of all Client-specific data including donor records, contribution records, service request records, volunteer records, and all other operational data stored within the LOTV platform.
- **Infrastructure Configurations:** All Client-specific cloud infrastructure configurations, database schemas, CI/CD pipelines, and deployment scripts transfer to Client upon final payment.
- **Design Assets:** All custom UI designs, branding assets, and graphic deliverables created for LOTV transfer to Client upon final payment.

**Third-Party Components:** The LOTV platform will incorporate open-source components and third-party libraries (e.g., .NET, xUnit, MudBlazor or Radzen, Stripe SDK). Client's use of such components is subject to their respective open-source or commercial licenses. WTE will provide a software bill of materials (SBOM) listing all third-party dependencies with their license types upon project completion.

---

## Confidentiality

**Mutual Obligations:** Both parties shall maintain the confidentiality of all non-public business, technical, or financial information exchanged in connection with the LOTV project, including but not limited to: platform architecture and source code, donor and beneficiary personal data, financial records, business processes, and pricing. Neither party shall disclose confidential information to third parties without written consent except as required to perform services under this Agreement.

**Client Data Privacy:** LOTV handles sensitive personal information including data belonging to people in need, donor financial information, and potentially information about vulnerable populations. WTE will implement and maintain industry-standard data security practices appropriate for a social services application, including encryption at rest and in transit, role-based access controls, and secure coding practices. WTE will execute any required Data Processing Agreement (DPA) as a sub-processor if Client is subject to GDPR, CCPA, or similar privacy regulations.

**Data Security:**
- Encryption at rest and in transit for all Client data
- Role-based access controls across all LOTV services
- Regular security assessments during development
- Incident notification within twenty-four (24) hours of any confirmed data security event

---

## Warranties and Disclaimers

**WTE Warranties:** Services are performed in a professional, workmanlike manner consistent with industry standards for .NET 9 SaaS application development. Personnel possess requisite skills and experience. Deliverables will not knowingly infringe third-party intellectual property rights. Code delivered under each SOW milestone will function materially as described in the applicable SOW for a period of thirty (30) days following Client acceptance ("Warranty Period"). WTE will correct any material defects reported during the Warranty Period at no additional charge.

**Client Warranties:** Client has authority to enter this Agreement. Client will provide timely, accurate requirements, feedback, and approvals as specified in active SOWs. Client will provide necessary system access, credentials, and test data. Client will comply with applicable laws in its use and operation of the LOTV platform.

**Disclaimer:** Except as expressly stated herein, all services are provided "as is." WTE disclaims all implied warranties of merchantability, fitness for a particular purpose, and uninterrupted or error-free operation. WTE is not responsible for third-party service outages (cloud providers, payment processors, email providers, SMS providers) that affect LOTV availability.

---

## Limitation of Liability

**Liability Cap:** WTE's total liability for any claims arising from this Agreement shall not exceed the lesser of: (a) total fees paid by Client to WTE in the twelve (12) months preceding the claim, or (b) $150,000 per occurrence, $300,000 aggregate annually.

**Excluded Damages:** WTE shall not be liable for indirect, incidental, consequential, special, or punitive damages, including loss of donations, loss of business, loss of data, or service interruption, regardless of legal theory, even if advised of the possibility of such damages.

**Exceptions:** Liability limitations do not apply to: intentional misconduct or gross negligence, breach of confidentiality obligations involving personally identifiable information, infringement of intellectual property rights, or violations of applicable law.

---

## Compliance and Security

**Regulatory Compliance:** The LOTV platform will be developed in accordance with:
- **Payment Processing:** PCI DSS requirements applicable to integration with Stripe or other payment processors
- **Privacy:** GDPR and CCPA principles for handling donor and beneficiary personal data
- **Accessibility:** WCAG 2.1 AA accessibility standards for the Blazor WebAssembly frontend
- **Nonprofit/Financial:** Best practices for charitable contribution tracking and tax receipt generation

**Security Standards:**
- Secure coding practices (OWASP Top 10 mitigations throughout development)
- Input validation and output encoding
- Authentication and authorization hardening (JWT, role-based access)
- Dependency vulnerability scanning (Dependabot or equivalent)
- HTTPS enforcement and security headers

**Data Residency:** All Client data will be maintained within the United States unless explicitly agreed otherwise in writing.

---

## Indemnification

Client agrees to indemnify and hold harmless WTE, its owners, employees, and agents from any loss, damage, liability, cost, or claim arising from: (a) Client's use of the LOTV platform in violation of this Agreement or applicable law; (b) Client-provided content, data, or specifications that infringe third-party rights; (c) Client's integration of LOTV with third-party systems not specified in an applicable SOW.

WTE agrees to indemnify and hold harmless Client from any third-party claims that the LOTV custom application code developed by WTE under this Agreement infringes any third-party intellectual property rights, provided that Client promptly notifies WTE of such claim and cooperates in the defense.

---

## Force Majeure

Neither party will be held liable for delays or non-performance resulting directly from causes beyond reasonable control including natural disasters, government actions, cyber-attacks, pandemic-related disruptions, or third-party infrastructure outages (cloud provider, payment processor). The affected party must promptly notify the other and use commercially reasonable efforts to mitigate impact and resume performance.

---

## General Provisions

**Governing Law:** This Agreement is governed by the laws of North Carolina without regard to conflict of law principles.

**Dispute Resolution:**
- **Mandatory Mediation:** Prior to initiating litigation, the parties agree to participate in good-faith mediation administered by the American Arbitration Association (AAA) in Raleigh, North Carolina. Each party bears its own mediation costs; mediator fees split equally.
- **Binding Arbitration:** If mediation fails within sixty (60) days, either party may demand binding arbitration under AAA Commercial Arbitration Rules in Wake County, North Carolina, before a single arbitrator with software industry experience.
- **Attorney's Fees:** The prevailing party in any dispute may recover reasonable attorney's fees and costs.
- **Equitable Relief:** Either party may seek immediate injunctive relief in any court of competent jurisdiction to protect intellectual property rights or confidential information without first pursuing mediation.

**Independent Contractor:** WTE provides all services as an independent contractor. Nothing in this Agreement creates a partnership, joint venture, or employer-employee relationship. Neither party has authority to bind the other.

**Amendment:** Modifications must be in writing and signed by authorized representatives of both parties.

**Notices:** Notices shall be in writing and delivered by hand, overnight courier, U.S. mail (return receipt requested), or confirmed email to the addresses in Attachment D.

**Assignment:** Neither party may assign or transfer this Agreement without prior written consent, except that WTE may assign in connection with a sale or merger of all or substantially all of its business.

**Severability:** If any provision is deemed unenforceable, the remainder of this Agreement remains in full effect.

**Entire Agreement:** This Agreement, together with all executed SOWs, EOWs, and Attachments, constitutes the complete agreement between the parties and supersedes all prior agreements regarding the LOTV project.

---

## Signature Block

| WTE Solutions, a PointShop, Inc. Company | [CLIENT ORGANIZATION NAME] |
|---|---|
| Signature: _________________________ | Signature: _________________________ |
| Name: Eric Garrison | Name: _________________________ |
| Title: President | Title: _________________________ |
| Date: _________________________ | Date: _________________________ |

---

## Attachment A: Technology Services Catalog

*See WTE Solutions standard Technology Services Catalog (incorporated by reference). Relevant service categories for the LOTV engagement include: Custom Software Development (.NET/C#, SaaS platform, RESTful API, Blazor), Data Services & Analytics (dashboard development, KPI systems), Cloud Services & Infrastructure (Azure App Services, Azure SQL, Azure Blob Storage, CI/CD), Payment Integration (Stripe), and Cybersecurity Solutions (OWASP hardening, HTTPS, security headers).*

---

## Attachment B: LOTV Technical Scope Summary

The LOTV platform is a multi-role .NET 9 SaaS application serving five user types: People in Need, Donors, Volunteers/Local Helpers, Staff/Employees, and Administrators. Core capability areas include:

1. Service Request Management with task assignment, status tracking, priority levels, due dates, and escalation
2. Donor Management with monetary and resource contribution tracking, tax receipt generation
3. Volunteer Coordination with location-based request matching and assignment workflow
4. Donation Tracking Dashboard — by person, diocese, city, amount band, and donation channel
5. Impact & Distribution Dashboard — where money and resources were sent, geographic map, timeline
6. Event Management — galas, silent auctions, dinners; ticket sales, attendee check-in, auction bidding
7. Staff Task Management — kanban board, workload view, request queue, notes and activity log
8. Notifications — email and SMS for all user types
9. Payment Processing — Stripe integration for donations and event tickets

Full technical scope is defined in `MASTER_TODO.md` maintained in the project repository.

---

## Attachment C: Rate Card

*[Reference WTE Solutions standard Rate Card. Emergency/After-Hours work billed at 2x standard hourly rate as defined in WTE's standard terms.]*

| Service Category | Rate |
|---|---|
| Software Development | $[RATE]/hr |
| Architecture & Design | $[RATE]/hr |
| Project Management | $[RATE]/hr |
| DevOps / Infrastructure | $[RATE]/hr |
| QA / Testing | $[RATE]/hr |
| Consulting / Advisory | $[RATE]/hr |
| Emergency / After-Hours | 2x applicable standard rate |

---

## Attachment D: Contact Information

**WTE Solutions, a PointShop, Inc. Company**
169 Boone Square Street, #230
Hillsborough, NC 27278
Phone: 866-994-7467
Email: info@wte.net
Billing: billing@wte.net
Project Manager: Chris Kremer

**[CLIENT ORGANIZATION NAME]**
[Address]
[City, State, ZIP]
Primary Contact: [Name], [Title]
Email: [Email]
Phone: [Phone]
Billing Contact: [Name / Email]
