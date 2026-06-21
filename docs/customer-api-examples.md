# Customer GraphQL And Download Examples

The examples assume the service is available at:

```text
http://localhost:5000/graphql
```

Authentication is currently disabled in the sample service.

Grid and export examples require an active `Customer/default` row in `app.gridSchema`. The current source does not register a Customer code seed, so provision that row explicitly on a fresh database.

## Customer Queries

### Default Page

Omitting paging arguments uses the configured default page size.

```graphql
query GetCustomers {
  customers {
    totalCount
    nodes {
      id
      name
      email
      birthDate
    }
  }
}
```

The service intentionally does not expose an unpaged customer collection.

`totalCount` is the total number of records matching the current filter, independent of `first`, `last`, `after`, and `before`. Page size is supplied by `first` or `last`; the configured default is 25 and maximum is 100.

### First Page Without A Cursor

```graphql
query GetFirstCustomerPage {
  customers(first: 25) {
    totalCount
    nodes {
      id
      name
      email
      birthDate
    }
    pageInfo {
      hasNextPage
      hasPreviousPage
      startCursor
      endCursor
    }
  }
}
```

### Next Page With A Cursor

```graphql
query GetNextCustomerPage($cursor: String!) {
  customers(first: 25, after: $cursor) {
    totalCount
    nodes {
      id
      name
      email
      birthDate
    }
    pageInfo {
      hasNextPage
      hasPreviousPage
      startCursor
      endCursor
    }
  }
}
```

Variables:

```json
{
  "cursor": "PASTE_THE_PREVIOUS_END_CURSOR_HERE"
}
```

### Previous Page With A Cursor

```graphql
query GetPreviousCustomerPage($cursor: String!) {
  customers(last: 25, before: $cursor) {
    totalCount
    nodes {
      id
      name
      email
    }
    pageInfo {
      hasNextPage
      hasPreviousPage
      startCursor
      endCursor
    }
  }
}
```

Use the previous response's `startCursor` as `cursor`.

### Sorted Customers

```graphql
query GetCustomersSortedByName {
  customers(first: 25, order: [{ name: ASC }]) {
    totalCount
    nodes {
      id
      name
      email
    }
    pageInfo {
      hasNextPage
      endCursor
    }
  }
}
```

### Filtered Customers

```graphql
query FindCustomersByName {
  customers(
    first: 25
    where: { name: { contains: "Customer" } }
    order: [{ name: ASC }]
  ) {
    totalCount
    nodes {
      id
      name
      email
      birthDate
    }
    pageInfo {
      hasNextPage
      endCursor
    }
  }
}
```

### Filter By Exact Email Using Variables

```graphql
query FindCustomerByEmail($email: String!) {
  customers(first: 1, where: { email: { eq: $email } }) {
    totalCount
    nodes {
      id
      name
      email
      birthDate
    }
  }
}
```

Variables:

```json
{
  "email": "customer0001@example.test"
}
```

### Grid Definition

```graphql
query GetCustomerGridDefinition {
  gridDefinition(entityName: "Customer", gridViewName: "default") {
    entityName
    gridViewName
    exportFormat
    columns {
      columnName
      columnType
      displayFormat
      textAlignment
      visibility
      width
      enableFiltering
      enableSorting
      sourceGraphqlColumn
    }
  }
}
```

## Customer Export Queries

Export queries create asynchronous jobs. GraphQL returns job metadata and a REST URL, not file content.

### CSV With Selected Columns

```graphql
query ExportCustomersToCsv {
  downloadCustomers(
    format: CSV
    columns: ["id", "name", "email", "birthDate"]
  ) {
    exportId
    status
    downloadUrl
    expiresUtc
  }
}
```

### Excel With Selected Columns

```graphql
query ExportCustomersToExcel {
  downloadCustomers(
    format: EXCEL
    columns: ["id", "name", "email"]
  ) {
    exportId
    status
    downloadUrl
    expiresUtc
  }
}
```

### Filtered And Ordered Export

Filters and ordering are stored in `job.yaml` and reapplied by the background worker before its server-side column projection.

```graphql
query ExportFilteredCustomers {
  downloadCustomers(
    format: CSV
    columns: ["id", "name", "email"]
    filters: [
      { field: "name", operator: CONTAINS, value: "Customer" }
    ]
    order: [
      { field: "name", direction: ASCENDING }
      { field: "email", direction: DESCENDING }
    ]
  ) {
    exportId
    status
    downloadUrl
    expiresUtc
  }
}
```

Supported sample filters are:

- `id`: `EQUAL`.
- `name` and `email`: `EQUAL` or `CONTAINS`.
- `birthDate`: `EQUAL`, `GREATER_THAN_OR_EQUAL`, or `LESS_THAN_OR_EQUAL` using `yyyy-MM-dd` values.

All customer fields support `ASCENDING` and `DESCENDING` ordering. The worker adds `id` as a deterministic final ordering key when it is not explicitly supplied.

### Export A Named Grid View

The named grid view determines visible columns, filters, ordering, and export format. A view without an export format uses `Export:DefaultFormat`.

```graphql
query ExportDefaultCustomerGrid {
  downloadCustomersByGridView(gridViewName: "default") {
    exportId
    status
    downloadUrl
    expiresUtc
  }
}
```

Example response:

```json
{
  "data": {
    "downloadCustomersByGridView": {
      "exportId": "9569eb16-1137-4934-8682-11c49003f648",
      "status": "QUEUED",
      "downloadUrl": "/exports/9569eb16-1137-4934-8682-11c49003f648/download",
      "expiresUtc": "2026-06-21T08:30:00Z"
    }
  }
}
```

## Customer Export Status Subscription

After creating an export job, subscribe with its UUID over the `graphql-ws` WebSocket protocol:

```graphql
subscription WatchCustomerExport($exportId: UUID!) {
  downloadCustomersStatus(exportId: $exportId) {
    exportId
    status
    downloadUrl
    expiresUtc
    errorCode
  }
}
```

Variables:

```json
{
  "exportId": "9569eb16-1137-4934-8682-11c49003f648"
}
```

The stream emits whenever the YAML status changes. `downloadUrl` remains `null` while the job is queued or running. When the worker records `COMPLETED`, the final event contains the REST download URL and the subscription closes:

```json
{
  "data": {
    "downloadCustomersStatus": {
      "exportId": "9569eb16-1137-4934-8682-11c49003f648",
      "status": "COMPLETED",
      "downloadUrl": "/exports/9569eb16-1137-4934-8682-11c49003f648/download",
      "expiresUtc": "2026-06-21T08:30:00Z",
      "errorCode": null
    }
  }
}
```

`FAILED`, `EXPIRED`, and `DOWNLOADED` are also terminal events. Failed events contain `errorCode` and no download URL.

## Download Exported Data

Use the `downloadUrl` returned by GraphQL:

```bash
curl --fail-with-body \
  --output customers.csv \
  http://localhost:5000/exports/9569eb16-1137-4934-8682-11c49003f648/download
```

For Excel:

```bash
curl --fail-with-body \
  --output customers.xlsx \
  http://localhost:5000/exports/9569eb16-1137-4934-8682-11c49003f648/download
```

Download responses depend on job status:

| Status | HTTP response |
| --- | --- |
| `QUEUED`, `CLAIMED`, `RUNNING` | `202 Accepted`; retry after the delay in `Retry-After` |
| `COMPLETED` | `200 OK` with the file attachment |
| `FAILED` | `422 Unprocessable Content` with a sanitized error code |
| Missing, expired, or removed | `404 Not Found` |

Example polling loop:

```bash
download_url="http://localhost:5000/exports/9569eb16-1137-4934-8682-11c49003f648/download"

while true; do
  status=$(curl --silent --show-error \
    --output customers.csv.part \
    --write-out '%{http_code}' \
    "$download_url")

  if [ "$status" = "200" ]; then
    mv customers.csv.part customers.csv
    break
  fi

  rm -f customers.csv.part
  if [ "$status" != "202" ]; then
    echo "Download failed with HTTP $status" >&2
    exit 1
  fi

  sleep 5
done
```

When `Export:DeleteJobFolderAfterDownload` is `true`, the service deletes the export job folder after successful response streaming.

## Customer Mutations

### Create Customer

```graphql
mutation CreateCustomer {
  createCustomer(
    input: {
      name: "Customer 0001"
      email: "customer0001@example.test"
      birthDate: "1990-01-01"
    }
  ) {
    id
    name
    email
    birthDate
  }
}
```

### Create Customer Using Variables

```graphql
mutation CreateCustomer($input: CustomerCreateInput!) {
  createCustomer(input: $input) {
    id
    name
    email
    birthDate
  }
}
```

Variables:

```json
{
  "input": {
    "name": "Customer 0002",
    "email": "customer0002@example.test",
    "birthDate": "1990-01-02"
  }
}
```

### Update Selected Customer Fields

Only supplied fields are changed.

```graphql
mutation UpdateCustomer($input: CustomerUpdateInput!) {
  updateCustomer(input: $input) {
    id
    name
    email
    birthDate
  }
}
```

Variables:

```json
{
  "input": {
    "id": "9569eb16-1137-4934-8682-11c49003f648",
    "name": "Updated Customer",
    "email": "updated.customer@example.test"
  }
}
```

### Clear A Nullable Birth Date

```graphql
mutation ClearCustomerBirthDate($input: CustomerUpdateInput!) {
  updateCustomer(input: $input) {
    id
    name
    birthDate
  }
}
```

Variables:

```json
{
  "input": {
    "id": "9569eb16-1137-4934-8682-11c49003f648",
    "birthDate": null
  }
}
```

### Delete Customer

The sample uses hard deletion. A successful mutation physically removes the database row and returns the removed entity.

```graphql
mutation DeleteCustomer($id: UUID!) {
  deleteCustomer(id: $id) {
    id
    name
    email
  }
}
```

Variables:

```json
{
  "id": "9569eb16-1137-4934-8682-11c49003f648"
}
```

### Duplicate Email Error

The database has a unique email index, but the current generic mutation does not translate PostgreSQL unique-constraint exceptions. The public response is therefore sanitized:

```json
{
  "errors": [
    {
      "message": "An internal error occurred.",
      "extensions": {
        "code": "INTERNAL_SERVER_ERROR"
      }
    }
  ],
  "data": null
}
```
