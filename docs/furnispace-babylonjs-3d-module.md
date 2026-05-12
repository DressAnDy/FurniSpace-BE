# FurniSpace - Định hướng triển khai module 3D bằng Babylon.js

## 1. Mục tiêu tài liệu

Tài liệu này tổng hợp phần nghiên cứu Babylon.js, lý do chọn Babylon.js thay vì Three.js cho FurniSpace, và phân chia công việc cần làm giữa Frontend và Backend để triển khai module thiết kế 3D.

Module 3D của FurniSpace cần phục vụ các mục tiêu chính:

- Cho phép khách hàng nhập kích thước không gian thực tế.
- Hiển thị cửa hàng/phòng dưới dạng 3D theo đúng tỷ lệ.
- Cho phép đặt, xoay, di chuyển, thay đổi vật liệu và tùy chỉnh nội thất.
- Kết nối thiết kế 3D với danh mục sản phẩm, báo giá, đơn hàng, sản xuất và giao hàng.
- Hỗ trợ người dùng không chuyên kỹ thuật thao tác dễ hiểu.

## 2. Kết luận lựa chọn công nghệ

Với phạm vi của FurniSpace, nên chọn **Babylon.js** cho module 3D.

Lý do chính là FurniSpace không chỉ cần hiển thị mô hình 3D, mà cần một môi trường thiết kế tương tác có đầy đủ các thao tác như nhập kích thước thật, đặt nội thất, chọn object, kéo thả, xoay, đổi vật liệu, kiểm tra độ vừa và render realtime. Babylon.js có nhiều thành phần hỗ trợ sẵn hơn cho kiểu hệ thống 3D editor/configurator.

Three.js vẫn là một thư viện rất mạnh, phổ biến và linh hoạt. Tuy nhiên, nếu dùng Three.js, nhóm sẽ phải tự xây hoặc tự ghép nhiều phần hơn như picking, transform controls, snapping, highlighting, validation, debug tooling và asset workflow. Với đồ án có giới hạn thời gian, Babylon.js giúp giảm khối lượng tự xây phần hạ tầng 3D.

Đề xuất câu mô tả công nghệ trong đồ án:

> Công nghệ 3D: sử dụng WebGL thông qua Babylon.js để xây dựng môi trường thiết kế 3D tương tác, hỗ trợ render realtime, import model nội thất, tùy chỉnh vật liệu, thao tác object và kiểm tra kích thước không gian.

## 3. Babylon.js phù hợp với FurniSpace như thế nào?

Babylon.js là một 3D engine chạy trên trình duyệt, hỗ trợ WebGL/WebGPU và có hệ sinh thái tương đối đầy đủ để xây dựng ứng dụng 3D tương tác. Đối với FurniSpace, Babylon.js có thể đảm nhiệm vai trò rendering layer và interaction layer cho module thiết kế cửa hàng/nội thất.

Các khả năng quan trọng:

- Render không gian 3D realtime trên website.
- Tạo phòng, sàn, tường, trần theo kích thước thực tế.
- Import mô hình nội thất từ file GLB/glTF, OBJ, STL.
- Tạo object mới bằng built-in mesh, parametric mesh hoặc custom mesh.
- Tùy chỉnh vật liệu, màu sắc, texture, ánh sáng và camera.
- Cho phép user chọn, kéo, xoay, scale object.
- Hỗ trợ debug scene bằng Babylon Inspector.
- Có công cụ hỗ trợ tối ưu như instances, LOD, asset manager và scene optimizer.

## 4. Quản lý kích thước và đơn vị đo

Babylon.js không ép buộc một đơn vị đo cố định. Scene trong Babylon.js về bản chất là `unitless`, nghĩa là ứng dụng tự quy ước một đơn vị nội bộ.

Ví dụ:

- `1 Babylon unit = 1 meter`
- hoặc `1 Babylon unit = 1 centimeter`
- hoặc một quy ước riêng của hệ thống.

Với FurniSpace, nên chọn một đơn vị nội bộ duy nhất, ví dụ `meter`, rồi convert toàn bộ input của user về đơn vị đó.

Ví dụ nếu chọn `1 unit = 1 meter`:

- Phòng dài 5m sẽ có chiều dài `5` trong scene.
- Tủ cao 2m sẽ có chiều cao `2` trong scene.
- Tường dày 10cm sẽ được convert thành `0.1` trong scene.

Nếu user nhập bằng feet/inch/cm/mm, hệ thống cần convert trước khi render:

- `300 cm` -> `3 meters`
- `120 inches` -> `3.048 meters`
- `10 feet` -> `3.048 meters`

Điểm quan trọng: Babylon.js render được theo kích thước, nhưng việc hiểu `m`, `cm`, `mm`, `feet`, `inch` là trách nhiệm của hệ thống. Vì vậy FurniSpace cần có một `UnitService` riêng.

## 5. Khả năng tạo và quản lý entity 3D

Babylon.js không bị giới hạn bởi các object có sẵn. Có nhiều cách tạo entity trong scene.

### Built-in mesh

Babylon.js có sẵn các hình cơ bản:

- Box
- Sphere
- Cylinder
- Plane
- Ground
- Torus
- Polygon
- Polyhedron

Trong FurniSpace, built-in mesh có thể dùng để tạo nhanh:

- Sàn nhà
- Tường
- Trần
- Cột
- Vách ngăn
- Placeholder cho nội thất
- Bậc thang đơn giản

### Parametric mesh

Babylon.js hỗ trợ các mesh được tạo từ tham số hoặc đường dẫn:

- Extrusion
- Lathe
- Tube
- Ribbon
- Custom path

Nhóm này hữu ích khi cần tạo các thành phần có hình dạng linh hoạt:

- Tường theo floor plan không phải hình chữ nhật.
- Len chân tường.
- Vách cong.
- Ray treo.
- Cầu thang.
- Chi tiết trang trí nội thất.

### Custom mesh

Khi built-in mesh và parametric mesh không đủ, Babylon.js cho phép tạo mesh từ dữ liệu vertex:

- Position
- Indices
- Normal
- UV
- Vertex color

Đây là mức tùy biến sâu nhất, phù hợp với các trường hợp:

- Mặt bằng cửa hàng phức tạp.
- Góc tường không vuông.
- Cửa hoặc cửa sổ cắt vào tường.
- Sàn nhiều cấp độ.
- Object nội thất được sinh theo tham số.

### Imported model

Babylon.js hỗ trợ import nhiều định dạng model 3D, trong đó nên ưu tiên:

- GLB/glTF cho catalog chính.
- OBJ hoặc STL cho một số trường hợp phụ.

Với FurniSpace, GLB/glTF là lựa chọn tốt hơn vì phù hợp với web, hỗ trợ material PBR, texture, hierarchy và animation tốt hơn.

Quy trình asset đề xuất:

1. Designer tạo model trong Blender, 3ds Max hoặc công cụ 3D khác.
2. Export sang GLB/glTF.
3. Upload asset lên storage/backend.
4. Backend lưu metadata: kích thước thật, giá, vật liệu, category, tùy chọn customize.
5. Frontend load model vào Babylon.js scene.
6. Hệ thống normalize scale để model khớp với kích thước thật.

## 6. Khả năng tùy chỉnh của Babylon.js

Babylon.js có khả năng tùy chỉnh rất rộng, phù hợp với một hệ thống thiết kế nội thất/cửa hàng.

### Tùy chỉnh geometry

Có thể:

- Tạo mesh mới.
- Thay đổi kích thước mesh.
- Clone hoặc instance object.
- Merge mesh để tối ưu.
- Dispose mesh khi xóa object.
- Rebuild mesh khi user thay đổi kích thước phòng.

Ứng dụng:

- Resize phòng.
- Kéo dài/rút ngắn tường.
- Thêm/xóa cửa.
- Tạo phòng từ floor plan.
- Thay đổi kích thước tủ, bàn, kệ.

### Tùy chỉnh material và texture

Có thể tùy chỉnh:

- Màu sắc.
- Texture gỗ, gạch, vải, kim loại, đá, kính.
- Độ bóng/mờ.
- Độ trong suốt.
- PBR material.
- Environment reflection.

Ứng dụng:

- Đổi màu tường.
- Đổi sàn gỗ sang gạch.
- Đổi chất liệu sofa.
- Preview nhiều phiên bản sản phẩm.
- Cho khách hàng chọn vật liệu trước khi báo giá.

### Tùy chỉnh ánh sáng

Babylon.js hỗ trợ nhiều loại ánh sáng:

- Hemispheric light.
- Directional light.
- Point light.
- Spot light.
- Environment lighting.
- Shadow.

Ứng dụng:

- Mô phỏng ánh sáng cửa hàng.
- Mô phỏng đèn trần, đèn bàn, đèn tường.
- Tạo cảm giác không gian thật hơn.
- Hỗ trợ designer đánh giá bố cục và thẩm mỹ.

### Tùy chỉnh camera

Có thể triển khai nhiều chế độ xem:

- Orbit view để xoay quanh không gian.
- First-person view để đi trong cửa hàng.
- Top view để xem mặt bằng.
- Focus view để zoom vào object đang chọn.

Ứng dụng:

- Khách hàng xem tổng quan.
- Designer chỉnh bố cục.
- Sales trình bày phương án với khách.
- Admin hoặc tư vấn kiểm tra thiết kế nhanh.

### Tùy chỉnh interaction

Babylon.js hỗ trợ picking, raycast và tương tác với mesh.

Có thể xây dựng:

- Click chọn object.
- Hover highlight.
- Drag/drop nội thất.
- Xoay object.
- Scale object.
- Snap vào sàn, tường hoặc grid.
- Kiểm tra va chạm đơn giản.
- Hiển thị guideline và measurement label.

Đây là nhóm tính năng cốt lõi vì FurniSpace không chỉ là viewer 3D, mà là công cụ thiết kế tương tác.

## 7. So sánh ngắn Babylon.js và Three.js

| Tiêu chí | Three.js | Babylon.js | Nhận xét |
|---|---|---|---|
| Render 3D realtime | Tốt | Tốt | Cả hai đều đáp ứng |
| Nhập kích thước thật | Làm được | Làm được | App phải tự quản lý unit |
| Tạo phòng, tường, sàn | Làm được | Làm được thuận tiện | Babylon.js có MeshBuilder/API editor-friendly |
| Import GLB/glTF | Tốt | Tốt | Cả hai đều phù hợp |
| Tùy chỉnh material | Tốt | Tốt | Babylon.js có tooling rõ hơn |
| Picking/highlight | Cần tự ghép nhiều hơn | Có sẵn nhiều tiện ích hơn | Babylon.js lợi thế |
| Kéo/xoay/scale object | Làm được | Làm được thuận tiện hơn | Babylon.js hợp MVP hơn |
| Debug scene | Cần helper riêng | Có Inspector mạnh | Babylon.js lợi thế |
| Tối ưu performance | Tốt nếu làm đúng | Tốt, có nhiều công cụ sẵn | Babylon.js dễ kiểm soát hơn |
| Độ nhẹ và linh hoạt | Rất tốt | Tốt | Three.js linh hoạt hơn |
| Phù hợp 3D editor/configurator | Tốt nhưng phải tự build nhiều | Rất phù hợp | Babylon.js nên ưu tiên |

Kết luận: Three.js phù hợp nếu cần thư viện 3D nhẹ, linh hoạt và phổ biến. Babylon.js phù hợp hơn nếu mục tiêu là xây dựng một hệ thống thiết kế 3D tương tác có workflow rõ ràng như FurniSpace.

## 8. Kiến trúc đề xuất cho module 3D

Nên tách rõ data của ứng dụng và scene Babylon.js.

Nguyên tắc quan trọng:

- Database và backend là nguồn dữ liệu chính.
- Babylon.js chỉ là lớp hiển thị và tương tác 3D.
- Không nên lưu kích thước thật chỉ trong mesh.
- Mọi object trong scene phải mapping được về entity trong database.
- Unit conversion phải nằm trong logic riêng, không rải rác trong code render.

Data model đề xuất:

- `Project`: thông tin dự án, khách hàng, trạng thái vòng đời.
- `Room`: kích thước phòng, đơn vị user chọn, chiều cao, độ dày tường, floor plan.
- `DesignScene`: snapshot dữ liệu thiết kế 3D của một project.
- `SceneEntity`: object được đặt trong scene, gồm product/custom item/procedural object.
- `Product`: sản phẩm nội thất trong catalog.
- `ProductAsset`: file GLB/glTF, thumbnail, texture, metadata kỹ thuật.
- `MaterialOption`: tùy chọn màu, texture, vật liệu, giá cộng thêm.
- `CustomizationRequest`: yêu cầu tùy chỉnh kích thước/vật liệu/bố cục.
- `Quote`: báo giá được tạo từ thiết kế.

Các service nên có:

- `UnitService`: convert đơn vị đo.
- `SceneService`: chuyển data thành scene Babylon.js.
- `AssetService`: load/cache/normalize model.
- `InteractionService`: chọn, kéo, xoay, snap, validate object.
- `PricingService`: tính giá từ product, vật liệu, kích thước và tùy chỉnh.
- `ValidationService`: kiểm tra kích thước, va chạm, độ vừa và tính khả thi.

## 9. Công việc Frontend cần làm

Frontend chịu trách nhiệm chính cho trải nghiệm người dùng, Babylon.js scene, thao tác thiết kế và kết nối dữ liệu với backend.

### 9.1. Tích hợp Babylon.js

- Cài đặt Babylon.js vào frontend project.
- Tạo component/module riêng cho 3D editor.
- Khởi tạo engine, canvas, scene, camera, light.
- Quản lý lifecycle: init, resize, render loop, dispose.
- Tách Babylon.js logic ra khỏi UI component để dễ bảo trì.

Kết quả mong đợi:

- Có màn hình 3D editor chạy ổn định trên browser.
- Scene có camera, light, grid/floor cơ bản.
- Không bị memory leak khi rời màn hình hoặc đổi project.

### 9.2. Render không gian theo kích thước user nhập

- Xây form nhập kích thước phòng/cửa hàng.
- Hỗ trợ đơn vị `m`, `cm`, `mm`, `ft`, `inch`.
- Gọi `UnitService` để convert sang đơn vị nội bộ.
- Tạo floor, wall, ceiling theo kích thước đã convert.
- Update scene khi user thay đổi kích thước.
- Hiển thị measurement label hoặc guideline nếu cần.

Kết quả mong đợi:

- User nhập kích thước và thấy không gian 3D đúng tỷ lệ.
- Có thể chỉnh kích thước phòng mà scene cập nhật theo.

### 9.3. Hiển thị catalog nội thất trong UI

- Xây sidebar/list catalog sản phẩm.
- Lọc theo category: bàn, ghế, kệ, quầy, đèn, decor.
- Hiển thị thumbnail, tên, kích thước, giá cơ bản.
- Gọi API backend để lấy danh sách sản phẩm.
- Cho phép user chọn sản phẩm và đặt vào scene.

Kết quả mong đợi:

- User có thể duyệt catalog và thêm nội thất vào thiết kế 3D.

### 9.4. Load và normalize model 3D

- Load file GLB/glTF từ URL backend trả về.
- Áp dụng scale để model đúng kích thước thật.
- Tạo bounding box để phục vụ chọn object và kiểm tra va chạm.
- Cache model hoặc asset đã load để tránh tải lại nhiều lần.
- Hiển thị loading state khi model đang tải.
- Xử lý fallback nếu model lỗi hoặc chưa có model 3D.

Kết quả mong đợi:

- Nội thất xuất hiện trong scene đúng tỷ lệ.
- Model không bị quá to/quá nhỏ do sai unit.

### 9.5. Tương tác với object trong scene

- Click để chọn object.
- Highlight object đang chọn.
- Kéo/thả object trên sàn.
- Xoay object theo trục Y.
- Scale hoặc resize nếu sản phẩm cho phép tùy chỉnh.
- Snap object vào grid, sàn hoặc tường.
- Hiển thị panel thông tin object đang chọn.
- Cho phép xóa object khỏi scene.

Kết quả mong đợi:

- User không chuyên kỹ thuật vẫn có thể bố trí nội thất dễ dàng.

### 9.6. Tùy chỉnh vật liệu và cấu hình sản phẩm

- Hiển thị các option vật liệu/màu sắc từ backend.
- Cho phép đổi material/texture trên model.
- Cho phép nhập kích thước tùy chỉnh nếu sản phẩm hỗ trợ.
- Validate giới hạn tùy chỉnh, ví dụ min/max width/height/depth.
- Cập nhật giá tạm tính khi thay đổi cấu hình.

Kết quả mong đợi:

- User có thể preview vật liệu và cấu hình trước khi gửi yêu cầu/báo giá.

### 9.7. Kiểm tra không gian và độ phù hợp

- Kiểm tra object có nằm ngoài phòng không.
- Kiểm tra object có chồng lên object khác không ở mức đơn giản.
- Kiểm tra khoảng cách tối thiểu nếu có rule từ backend.
- Cảnh báo khi nội thất không phù hợp kích thước phòng.
- Gửi dữ liệu scene cho backend để validate sâu hơn nếu cần.

Kết quả mong đợi:

- Thiết kế có phản hồi trực quan về lỗi bố trí hoặc lỗi kích thước.

### 9.8. Lưu, tải và đồng bộ thiết kế

- Serialize scene thành JSON app-defined, không phụ thuộc hoàn toàn vào Babylon mesh.
- Gửi dữ liệu thiết kế lên backend.
- Load lại thiết kế từ backend và reconstruct scene.
- Hỗ trợ autosave hoặc manual save.
- Quản lý version/draft nếu scope cho phép.

Kết quả mong đợi:

- User có thể lưu bản nháp, quay lại chỉnh sửa và tiếp tục quy trình báo giá.

### 9.9. Tối ưu trải nghiệm và hiệu năng

- Lazy-load model khi cần.
- Giảm chất lượng shadow/post-processing trên thiết bị yếu.
- Dùng instance/clone cho object lặp lại.
- Dispose asset không dùng.
- Hiển thị loading/progress rõ ràng.
- Đảm bảo responsive layout cho desktop/tablet.

Kết quả mong đợi:

- Module 3D chạy mượt trong phạm vi MVP và không làm nghẽn toàn bộ ứng dụng.

## 10. Công việc Backend cần làm

Backend chịu trách nhiệm lưu trữ dữ liệu thật, quản lý catalog, project, asset, validation, báo giá, phân quyền và tích hợp các module nghiệp vụ.

### 10.1. Thiết kế database cho module 3D

Cần thiết kế các bảng/entity chính:

- `Projects`: dự án thiết kế của khách hàng.
- `Rooms`: thông tin không gian, kích thước, unit, floor plan.
- `DesignScenes`: dữ liệu scene đã lưu.
- `SceneEntities`: object được đặt trong scene.
- `Products`: sản phẩm nội thất.
- `ProductAssets`: file model 3D, thumbnail, texture.
- `MaterialOptions`: tùy chọn vật liệu/màu sắc.
- `CustomizationRequests`: yêu cầu tùy chỉnh.
- `Quotes`: báo giá dựa trên thiết kế.

Kết quả mong đợi:

- Có schema đủ để lưu thiết kế 3D và kết nối với catalog, báo giá, đơn hàng.

### 10.2. API quản lý project và room

Cần cung cấp API:

- Tạo project draft.
- Cập nhật thông tin project.
- Lưu kích thước phòng/cửa hàng.
- Lưu floor plan nếu có.
- Lấy lại project để frontend reconstruct scene.
- Quản lý trạng thái project: draft, reviewing, quoted, ordered, in production, delivered.

Kết quả mong đợi:

- Frontend có thể tạo, lưu và mở lại thiết kế theo vòng đời dự án.

### 10.3. API catalog nội thất

Cần cung cấp API:

- Danh sách sản phẩm.
- Chi tiết sản phẩm.
- Lọc theo category, style, material, price range.
- Trả về kích thước thật của sản phẩm.
- Trả về URL model GLB/glTF, thumbnail, texture.
- Trả về option tùy chỉnh và giới hạn tùy chỉnh.

Kết quả mong đợi:

- Frontend có đủ dữ liệu để hiển thị catalog và load model vào scene.

### 10.4. Quản lý asset 3D

Backend cần quản lý:

- Upload file GLB/glTF.
- Upload thumbnail.
- Upload texture/material maps nếu có.
- Lưu metadata của asset: bounding size, unit gốc, polygon count nếu có.
- Cung cấp URL an toàn để frontend load asset.
- Kiểm tra file hợp lệ ở mức cơ bản.

Kết quả mong đợi:

- Mỗi sản phẩm trong catalog có asset 3D rõ ràng và có metadata để scale đúng.

### 10.5. Lưu và version dữ liệu scene

Backend không nên lưu trực tiếp object Babylon.js. Nên lưu JSON theo data model của FurniSpace.

Ví dụ dữ liệu cần lưu:

- Room dimensions.
- Unit đang dùng.
- Danh sách entity trong scene.
- Product ID hoặc custom entity type.
- Position, rotation, scale.
- Selected material.
- Custom dimensions.
- Notes hoặc yêu cầu tùy chỉnh.

Kết quả mong đợi:

- Thiết kế có thể load lại độc lập với implementation chi tiết của Babylon.js.

### 10.6. Validation phía backend

Frontend có thể validate nhanh, nhưng backend vẫn cần validate lại các rule quan trọng:

- Kích thước phòng hợp lệ.
- Sản phẩm có còn tồn tại không.
- Kích thước custom có nằm trong giới hạn sản xuất không.
- Object có vượt quá giới hạn không gian không ở mức dữ liệu.
- Vật liệu/tùy chọn có khả dụng không.
- Giá và rule báo giá có còn đúng không.

Kết quả mong đợi:

- Tránh việc user chỉnh dữ liệu client-side sai rồi gửi báo giá/đơn hàng không hợp lệ.

### 10.7. Tính giá và tạo báo giá

Backend cần xử lý:

- Giá cơ bản sản phẩm.
- Giá theo vật liệu.
- Giá theo kích thước tùy chỉnh.
- Phụ phí sản xuất/lắp đặt/giao hàng nếu có.
- Chiết khấu hoặc thuế nếu scope yêu cầu.
- Tạo quote từ thiết kế.
- Lưu lịch sử quote.

Kết quả mong đợi:

- Thiết kế 3D có thể chuyển thành báo giá có cơ sở dữ liệu rõ ràng.

### 10.8. Phân quyền và workflow nghiệp vụ

Backend cần áp dụng RBAC:

- Khách hàng: tạo project, chỉnh draft, gửi yêu cầu, xem báo giá.
- Sales/Tư vấn: xác nhận thông tin, tạo/chỉnh báo giá, theo dõi thanh toán.
- Designer: tinh chỉnh thiết kế, cập nhật layout, xử lý custom.
- Sản xuất: xem thông số kỹ thuật, xác nhận khả thi, cập nhật sản xuất.
- Giao hàng: cập nhật trạng thái giao hàng.
- Admin: quản lý user, catalog, giá, dữ liệu hệ thống.

Kết quả mong đợi:

- Module 3D không đứng riêng lẻ, mà kết nối đúng với quy trình FurniSpace.

### 10.9. Bảo mật và độ tin cậy

Backend cần đảm bảo:

- Xác thực và phân quyền API.
- Không cho user truy cập project không thuộc quyền.
- Validate dữ liệu đầu vào.
- Lưu transaction quan trọng an toàn.
- Không lưu thông tin thanh toán nhạy cảm sai cách.
- Backup dữ liệu project/design.
- Log lỗi khi load/save scene hoặc tạo quote.

Kết quả mong đợi:

- Dữ liệu thiết kế, đơn hàng và thanh toán an toàn, phù hợp yêu cầu phi chức năng.

## 11. API tối thiểu đề xuất cho MVP

Các API có thể bắt đầu ở mức tối thiểu:

- `POST /projects`: tạo project draft.
- `GET /projects/{id}`: lấy thông tin project.
- `PUT /projects/{id}`: cập nhật project.
- `PUT /projects/{id}/room`: lưu thông tin room.
- `GET /projects/{id}/scene`: lấy scene JSON.
- `PUT /projects/{id}/scene`: lưu scene JSON.
- `GET /products`: lấy danh sách catalog.
- `GET /products/{id}`: lấy chi tiết sản phẩm.
- `GET /materials`: lấy danh sách vật liệu.
- `POST /projects/{id}/validate-scene`: validate thiết kế.
- `POST /projects/{id}/quote`: tạo báo giá từ thiết kế.

## 12. Phân chia trách nhiệm FE và BE

| Hạng mục | Frontend | Backend |
|---|---|---|
| Nhập kích thước phòng | Form nhập liệu, UI unit | Lưu room data, validate input |
| Convert đơn vị | Convert để render nhanh | Validate lại và lưu canonical unit |
| Render 3D | Babylon.js scene | Không render trực tiếp |
| Catalog nội thất | Hiển thị, filter, chọn sản phẩm | Cung cấp product data và asset URLs |
| Load model 3D | Load GLB/glTF vào scene | Lưu asset, metadata, URL |
| Đặt object vào scene | Position, rotation, scale, interaction | Lưu SceneEntity |
| Tùy chỉnh vật liệu | Preview material trong scene | Quản lý material option và giá |
| Kiểm tra độ vừa | Cảnh báo realtime cơ bản | Validate rule quan trọng |
| Lưu thiết kế | Serialize scene JSON | Lưu, version, phân quyền |
| Báo giá | Hiển thị giá tạm tính | Tính giá chính thức |
| Workflow dự án | UI theo role/status | Quản lý trạng thái và quyền |

## 13. Scope MVP đề xuất cho module 3D

Để phù hợp với đồ án tốt nghiệp và giảm rủi ro, MVP nên tập trung vào:

- Tạo project draft.
- Nhập kích thước phòng hình chữ nhật.
- Render sàn, tường, trần theo kích thước.
- Hiển thị catalog nội thất.
- Load sản phẩm GLB/glTF vào scene.
- Đặt, kéo, xoay, xóa object.
- Đổi màu/vật liệu cơ bản.
- Kiểm tra object có vượt ra ngoài phòng không.
- Lưu/load thiết kế.
- Tạo báo giá cơ bản từ danh sách sản phẩm trong scene.

Các tính năng nên để giai đoạn sau:

- Floor plan tự do/phức tạp.
- Cắt cửa/cửa sổ trực tiếp vào tường.
- Collision vật lý phức tạp.
- Render photorealistic nặng.
- AR/VR.
- Multi-user realtime editing.
- Tự động đề xuất layout bằng AI.

## 14. Rủi ro kỹ thuật và cách xử lý

### Sai scale giữa các model

Rủi ro: model từ nhiều nguồn có unit khác nhau, dẫn đến nội thất quá to hoặc quá nhỏ.

Cách xử lý:

- Mỗi asset phải có metadata kích thước thật.
- Backend lưu `sourceUnit` và `realWidth/realHeight/realDepth`.
- Frontend normalize model bằng bounding box khi load.

### Scene quá nặng trên mobile

Rủi ro: model nhiều polygon, texture lớn, shadow nặng làm giảm FPS.

Cách xử lý:

- Dùng GLB đã tối ưu.
- Giới hạn texture size.
- Lazy-load asset.
- Dùng instance cho object lặp lại.
- Giảm shadow/post-processing trên thiết bị yếu.

### Lưu scene phụ thuộc quá nhiều vào Babylon.js

Rủi ro: dữ liệu khó migrate, khó tính báo giá, khó validate.

Cách xử lý:

- Lưu scene bằng JSON của FurniSpace.
- Mesh Babylon.js chỉ là representation trên frontend.
- Mỗi object trong scene phải có `entityId`, `productId`, position, rotation, scale, material.

### Validation chỉ nằm ở frontend

Rủi ro: dữ liệu có thể bị chỉnh từ client rồi gửi lên backend.

Cách xử lý:

- Frontend validate realtime để UX tốt.
- Backend validate lại trước khi tạo quote/order.

## 15. Nguồn tham khảo

- Babylon.js official docs: https://doc.babylonjs.com/
- Babylon.js specifications: https://www.babylonjs.com/specifications/
- Babylon.js GitHub: https://github.com/BabylonJS/Babylon.js/
- Babylon.js forum về unit: https://forum.babylonjs.com/t/what-is-the-unit-of-size-width-length-for-predefined-mesh-e-g-plane-in-babylon-js/27743
- Babylon.js guide về custom mesh: https://babylonjsguide.github.io/advanced/Custom
- Three.js official docs: https://threejs.org/docs/
- Three.js GLTFLoader docs: https://threejs.org/docs/pages/GLTFLoader.html
