# Hướng Dẫn Kết Nối MongoDB Cho Room Planner

Tài liệu này hướng dẫn cách kết nối MongoDB dùng cho module Room Planner.

## Cấu Hình Hiện Tại

MongoDB đã được cấu hình trong `docker-compose.yml` với service tên là `mongodb`.

```text
Tên container: furnispace-mongodb
Docker service: mongodb
Port trong container: 27017
Port trên máy local: 27018
Database: furnispace_room_planner
Collection: room_planner_scenes
```

Port trên máy local đang dùng `27018` vì máy có thể đã có MongoDB chạy trực tiếp ở `localhost:27017`.

## Chạy MongoDB Bằng Docker

Tại thư mục gốc của project, chạy:

```powershell
docker compose up -d mongodb
```

Kiểm tra trạng thái container:

```powershell
docker compose ps mongodb
```

## Kết Nối Bằng MongoDB Compass

Mở MongoDB Compass và tạo connection mới với URI:

```text
mongodb://localhost:27018
```

Setup Docker local hiện tại không cần username/password.

Sau khi kết nối thành công, nếu chưa có database thì tạo mới:

```text
Database Name: furnispace_room_planner
Collection Name: room_planner_scenes
```

## Cấu Hình Backend

File `.env` local cần có:

```env
MONGODB_CONNECTION_STRING=mongodb://localhost:27018
MONGODB_DATABASE_NAME=furnispace_room_planner
MONGODB_ROOM_PLANNER_SCENES_COLLECTION=room_planner_scenes
MONGODB_HOST_PORT=27018
MONGODB_CONTAINER_PORT=27017
DOCKER_MONGODB_CONNECTION_STRING=mongodb://mongodb:27017
```

Khi chạy API trực tiếp trên máy local, backend dùng:

```text
mongodb://localhost:27018
```

Khi chạy API trong Docker Compose, backend dùng:

```text
mongodb://mongodb:27017
```

## Quy Tắc Lưu Trữ Dữ Liệu

MongoDB chỉ dùng để lưu trạng thái visual/editor của Room Planner (schema v3), ví dụ:

```text
schemaVersion = 3
blueprintLayout.floors[]   (source of truth cho multi-floor)
  points / walls / doors / windows / openings
  elevation / floorHeight / projectAreaId
objects[] (mỗi object có floorId)
camera / lighting / validation / editorState
blueprintLayout.metadata   (free-form JSON, không phải source of truth)
```

Legacy root `layout` không còn được yêu cầu cho Room Planner multi-floor; BE clear `layout` khi save schema v3.

PostgreSQL vẫn là nguồn dữ liệu chính cho business data như:

```text
projects
proposals
proposal_scenes
proposal_scene_areas
proposal_items
products
product_versions
quotations
orders
payments
production data
```

External storage vẫn dùng để lưu file thật như hình ảnh, file model 3D, texture và file upload của project.

## Mapping Giữa SQL Và MongoDB

```text
SQL proposal_scenes.scene_id              -> MongoDB room_planner_scenes.sqlSceneId
SQL proposal_scenes.mongo_scene_id        -> MongoDB room_planner_scenes._id
SQL proposal_scene_areas.project_area_id  -> MongoDB blueprintLayout.floors[].projectAreaId
                                            + sceneLinks.projectAreaIds
```

Một record SQL trong bảng `proposal_scenes` sẽ map với một document chính thức trong collection `room_planner_scenes`. Mỗi floor trong `blueprintLayout.floors[]` map 1-1 với SQL `proposal_scene_areas`.

`pointId` / `wallId` / `openingId` chỉ cần unique trong cùng một floor (không validate global trên toàn scene).

## Xử Lý Lỗi Thường Gặp

Nếu MongoDB Compass không kết nối được `mongodb://localhost:27018`, kiểm tra container:

```powershell
docker compose ps mongodb
```

Nếu port `27018` đã bị process khác chiếm, đổi `MONGODB_HOST_PORT` trong `.env`, sau đó chạy lại:

```powershell
docker compose up -d mongodb
```

Nếu API chạy local nhưng không kết nối được MongoDB, kiểm tra `.env`:

```env
MONGODB_CONNECTION_STRING=mongodb://localhost:27018
```

Nếu API chạy trong Docker nhưng không kết nối được MongoDB, kiểm tra `docker-compose.yml` đang truyền:

```env
MONGODB_CONNECTION_STRING=mongodb://mongodb:27017
```
