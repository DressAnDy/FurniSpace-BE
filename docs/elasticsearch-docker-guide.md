# FurniSpace Elasticsearch Docker Guide

Tai lieu nay huong dan them Elasticsearch vao FurniSpace backend bang Docker. Project hien tai la .NET 8 backend theo huong DDD/layered architecture, da co PostgreSQL va Redis trong `docker-compose.yml`, nhung chua co search module hoac Elasticsearch client trong code.

## 1. Nen dung Elasticsearch cho viec gi

Elasticsearch phu hop cho cac use case sau cua FurniSpace:

- Tim kiem san pham noi that theo ten, mo ta, danh muc, vat lieu, mau sac.
- Filter/sort catalog theo gia, kich thuoc, tag, trang thai.
- Suggest/autocomplete khi nguoi dung tim san pham trong module thiet ke 3D.
- Tim kiem project/order/design ve sau neu du lieu lon va can full-text search.

Khong nen dung Elasticsearch lam database chinh. PostgreSQL van la source of truth. Elasticsearch chi nen la read model/search index, duoc dong bo tu PostgreSQL thong qua application event, background job, hoac explicit indexing sau khi ghi DB thanh cong.

## 2. Trang thai project hien tai

Da co:

- `docker-compose.yml` voi `api`, `postgres`, `redis`.
- `.env` duoc nap vao API container qua `env_file: .env`.
- `FurniSpace.Infrastructure` la noi dang ky cac external adapters nhu Redis, JWT, EF Core.
- `FurniSpace.Application` la noi nen dat interface search, vi Application khong nen phu thuoc truc tiep vao Elasticsearch SDK.

Chua co:

- Docker service `elasticsearch`.
- Docker service `kibana`.
- Elasticsearch env vars.
- NuGet package `Elastic.Clients.Elasticsearch`.
- Interface search trong Application.
- Implementation Elasticsearch trong Infrastructure.
- Product/Furniture catalog entity hoan chinh de index.

## 3. Docker Compose cho local development

Them service sau vao `docker-compose.yml`.

```yaml
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.19.15
    container_name: furnispace-elasticsearch
    environment:
      discovery.type: single-node
      xpack.security.enabled: "false"
      xpack.security.enrollment.enabled: "false"
      ES_JAVA_OPTS: "-Xms512m -Xmx512m"
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch_data:/usr/share/elasticsearch/data
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://localhost:9200/_cluster/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10
    networks:
      - furnispace-network
```

Them Kibana neu can UI de xem index, query va debug mapping:

```yaml
  kibana:
    image: docker.elastic.co/kibana/kibana:8.19.15
    container_name: furnispace-kibana
    environment:
      ELASTICSEARCH_HOSTS: http://elasticsearch:9200
    ports:
      - "5601:5601"
    depends_on:
      elasticsearch:
        condition: service_healthy
    networks:
      - furnispace-network
```

Them Elasticsearch vao `api.depends_on`:

```yaml
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      elasticsearch:
        condition: service_healthy
```

Them volume:

```yaml
volumes:
  postgres_data:
  redis_data:
  elasticsearch_data:
```

Ghi chu:

- Cau hinh tren tat security de don gian hoa local development. Khong dung y nguyen cho production.
- Elasticsearch can RAM tuong doi cao. Neu may yeu, giu `ES_JAVA_OPTS=-Xms512m -Xmx512m`; neu may khoe, co the tang len `1g`.
- Elastic khuyen nghi disable swapping cho moi truong nghiem tuc. Local dev co the bo qua neu chi chay demo.

## 4. Bien moi truong

Them vao `.env`:

```env
ELASTICSEARCH_URL=http://elasticsearch:9200
ELASTICSEARCH_INDEX_PREFIX=furnispace
```

Neu chay API ngoai Docker tren host machine, URL se la:

```env
ELASTICSEARCH_URL=http://localhost:9200
```

Co the dung appsettings neu muon cau truc ro rang hon:

```json
{
  "Elasticsearch": {
    "Url": "http://elasticsearch:9200",
    "IndexPrefix": "furnispace"
  }
}
```

Trong Docker Compose, dung environment variables se dong nhat voi Redis/PostgreSQL hien tai hon.

## 5. Lenh chay va kiem tra

Start stack:

```bash
docker compose up -d elasticsearch kibana
```

Kiem tra Elasticsearch:

```bash
curl http://localhost:9200
curl http://localhost:9200/_cluster/health?pretty
```

Kiem tra index:

```bash
curl http://localhost:9200/_cat/indices?v
```

Mo Kibana:

```text
http://localhost:5601
```

Neu chay toan bo backend:

```bash
docker compose up -d --build
```

## 6. NuGet package

Them official .NET client vao Infrastructure:

```bash
dotnet add src/FurniSpace.Infrastructure package Elastic.Clients.Elasticsearch
```

Khong add package nay vao Application hoac Domain. Application chi nen dinh nghia interface search.

## 7. Cau truc code de tich hop

De giu dung kien truc hien tai, nen them cac file sau:

```text
src/FurniSpace.Application/
  Common/Search/
    ElasticsearchSettings.cs
  Interfaces/
    ISearchIndexService.cs

src/FurniSpace.Infrastructure/
  Search/
    ElasticsearchIndexService.cs
```

Settings:

```csharp
namespace FurniSpace.Application.Common.Search;

public sealed class ElasticsearchSettings
{
    public const string SectionName = "Elasticsearch";

    public string Url { get; set; } = string.Empty;
    public string IndexPrefix { get; set; } = "furnispace";
}
```

Interface:

```csharp
namespace FurniSpace.Application.Interfaces;

public interface ISearchIndexService
{
    Task IndexAsync<TDocument>(
        string indexName,
        string id,
        TDocument document,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string indexName,
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(
        string indexName,
        string query,
        CancellationToken cancellationToken = default);
}
```

Implementation skeleton:

```csharp
using Elastic.Clients.Elasticsearch;
using FurniSpace.Application.Interfaces;

namespace FurniSpace.Infrastructure.Search;

public sealed class ElasticsearchIndexService : ISearchIndexService
{
    private readonly ElasticsearchClient _client;

    public ElasticsearchIndexService(ElasticsearchClient client)
    {
        _client = client;
    }

    public async Task IndexAsync<TDocument>(
        string indexName,
        string id,
        TDocument document,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.IndexAsync(document, indexName, id, cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }
    }

    public async Task DeleteAsync(
        string indexName,
        string id,
        CancellationToken cancellationToken = default)
    {
        await _client.DeleteAsync<object>(indexName, id, cancellationToken);
    }

    public async Task<IReadOnlyList<TDocument>> SearchAsync<TDocument>(
        string indexName,
        string query,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchAsync<TDocument>(s => s
            .Indices(indexName)
            .Query(q => q
                .QueryString(qs => qs.Query(query))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }

        return response.Documents.ToArray();
    }
}
```

## 8. Dependency Injection

Trong `src/FurniSpace.Infrastructure/DependencyInjection.cs`, them registration rieng, tuong tu cach project dang lam voi Redis:

```csharp
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using FurniSpace.Application.Common.Search;
using FurniSpace.Application.Interfaces;
using FurniSpace.Infrastructure.Search;
```

Trong `AddInfrastructure`:

```csharp
services.Configure<ElasticsearchSettings>(
    configuration.GetSection(ElasticsearchSettings.SectionName));

services.AddElasticsearch(configuration);
```

Them private method:

```csharp
private static IServiceCollection AddElasticsearch(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var url = configuration.GetSection(ElasticsearchSettings.SectionName)["Url"]
        ?? configuration["ELASTICSEARCH_URL"];

    if (string.IsNullOrWhiteSpace(url))
    {
        throw new InvalidOperationException(
            "Elasticsearch URL is missing. Set Elasticsearch__Url or ELASTICSEARCH_URL.");
    }

    var settings = new ElasticsearchClientSettings(new Uri(url))
        .DefaultIndex("furnispace");

    services.AddSingleton(new ElasticsearchClient(settings));
    services.AddScoped<ISearchIndexService, ElasticsearchIndexService>();

    return services;
}
```

Neu muon Elasticsearch la optional trong local dev, khong throw exception khi missing URL. Tuy nhien neu search la feature chinh cua catalog, fail fast se tot hon de tranh loi runtime kho debug.

## 9. Index naming

Nen dung prefix theo app/environment:

```text
furnispace-dev-furniture-products
furnispace-prod-furniture-products
```

Quy uoc de xuat:

```text
{ELASTICSEARCH_INDEX_PREFIX}-{environment}-{module}
```

Vi du:

```text
furnispace-dev-products
furnispace-dev-projects
furnispace-dev-designs
```

Khong index cac field nhay cam nhu password hash, refresh token, access token, thong tin thanh toan day du.

## 10. Search document cho furniture catalog

Khi co Product/Furniture entity, nen tao read model rieng thay vi index EF entity truc tiep:

```csharp
public sealed record FurnitureProductSearchDocument(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string Material,
    string Color,
    decimal Price,
    decimal Width,
    decimal Height,
    decimal Depth,
    bool IsActive);
```

Ly do:

- Tach search schema khoi database schema.
- Khong vo tinh dua field nhay cam len Elasticsearch.
- De toi uu field cho autocomplete/filter/sort.

## 11. Dong bo du lieu

Giai doan dau co the lam don gian:

- Sau khi tao/cap nhat product thanh cong trong handler, goi `ISearchIndexService.IndexAsync`.
- Sau khi xoa/disable product, goi `DeleteAsync` hoac index lai voi `IsActive = false`.

Khi du lieu lon hon, nen chuyen sang background job/outbox:

```text
ProductCreated/Updated domain event
  -> save DB transaction
  -> outbox/background worker
  -> index Elasticsearch
```

Quy tac quan trong:

- Database write phai thanh cong truoc.
- Neu index fail, khong nen rollback order/payment critical flow.
- Can co job reindex de rebuild Elasticsearch tu PostgreSQL.

## 12. Mapping de xuat

Cho product catalog, nen co mapping rieng:

```json
{
  "mappings": {
    "properties": {
      "id": { "type": "keyword" },
      "name": {
        "type": "text",
        "fields": {
          "keyword": { "type": "keyword" }
        }
      },
      "description": { "type": "text" },
      "category": { "type": "keyword" },
      "material": { "type": "keyword" },
      "color": { "type": "keyword" },
      "price": { "type": "double" },
      "width": { "type": "double" },
      "height": { "type": "double" },
      "depth": { "type": "double" },
      "isActive": { "type": "boolean" }
    }
  }
}
```

Lenh tao index local:

```bash
curl -X PUT http://localhost:9200/furnispace-dev-products \
  -H "Content-Type: application/json" \
  -d @products-index.json
```

## 13. Production notes

Khac voi local development, production can:

- Bat security va authentication.
- Dung HTTPS/TLS.
- Khong expose port `9200` public internet.
- Dat password/API key qua secret manager, khong commit vao repo.
- Cau hinh memory, disk, snapshot backup va retention.
- Monitoring cluster health, JVM heap, disk watermark, indexing/search latency.
- Xem xet Elastic Cloud hoac managed Elasticsearch neu team khong muon van hanh cluster.

## 14. Tai lieu tham khao

- Elastic Docker install docs: https://www.elastic.co/guide/en/elasticsearch/reference/current/docker.html
- Elastic .NET client docs: https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/index.html
- Docker Compose docs: https://docs.docker.com/compose/
