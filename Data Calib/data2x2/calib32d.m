clc; clear; close all;

%% ================== FILE & SETUP ==================
% Danh sách file thực tế của bạn
files = { ...
'A1(-0.6;0).txt','A2(0;0).txt','A3(0.6;0).txt','A4(1.2;0).txt','A5(1.8;0).txt', ...
'A6(2;0).txt','A7(2.4;0).txt','A8(2.4;0.6).txt','A9(2;0.6).txt','A10(2;1.2).txt', ...
'A11(2.4;1.2).txt','A12(2.4;1.8).txt','A13(2.4;2).txt','A14(2;2).txt','A15(2;1.8).txt', ...
'A16(1.8;2).txt','A17(1.2;2).txt','A18(0.6;2).txt','A19(0;2).txt','A20(0;1.8).txt', ...
'A21(0;1.2).txt','A22(0;0.6).txt','A23(0;-0.8).txt','A24(0.6;-0.8).txt','A25(1.2;-0.8).txt','A26(1.8;-0.8).txt', ...
'A27(2.4;-0.8).txt','A28(3.0;-0.8).txt','A29(3.0;0).txt','A30(3.0;0.6).txt','A31(2.4;0.6).txt', ...
'A32(3.0;1.2).txt'};

n = length(files);
X = zeros(n,1);
Y = zeros(n,1);

% TRÍCH XUẤT TOẠ ĐỘ THỰC TỪ TÊN FILE
% Cấu trúc: Axx(X;Y).txt -> Lưu ý dấu phẩy trong tên file của bạn (ví dụ 2,4 thay vì 2.4)
for i = 1:n
    % Tìm nội dung trong ngoặc đơn (...)
    tokens = regexp(files{i}, '\((.*?);(.*?)\)', 'tokens');
    if ~isempty(tokens)
        % Thay dấu phẩy thành dấu chấm để chuyển sang số (vd: 2,4 -> 2.4)
        strX = strrep(tokens{1}{1}, ',', '.');
        strY = strrep(tokens{1}{2}, ',', '.');
        X(i) = str2double(strX);
        Y(i) = str2double(strY);
    end
end

%% ================== CẤU HÌNH ANCHOR ==================
A1 = [0 4];
A2 = [4 0];
h_anchor = 1.7;
h_tag    = 0.25;
dz = h_anchor - h_tag;

%% ================== ĐỌC VÀ LỌC DỮ LIỆU (MAD FILTER) ==================
R1_raw = zeros(n,1);
R2_raw = zeros(n,1);
rej_all1 = zeros(n,1);
rej_all2 = zeros(n,1);

for i = 1:n
    % Kiểm tra file tồn tại trước khi đọc
    if ~exist(files{i}, 'file')
        warning('File %s không tìm thấy!', files{i});
        continue;
    end
    
    txt = fileread(files{i});
    r1_matches = regexp(txt,'R1=([\d\.]+)','tokens');
    r2_matches = regexp(txt,'R2=([\d\.]+)','tokens');
    
    r1 = cellfun(@str2double, [r1_matches{:}]);
    r2 = cellfun(@str2double, [r2_matches{:}]);

    % Hàm lọc MAD nội bộ để code gọn hơn
    filter_mad = @(data) data(abs(data - median(data)) <= 3 * max(median(abs(data - median(data))), 1e-6));
    
    r1_clean = filter_mad(r1);
    if isempty(r1_clean), r1_clean = r1; end
    
    r2_clean = filter_mad(r2);
    if isempty(r2_clean), r2_clean = r2; end

    R1_raw(i) = mean(r1_clean);
    R2_raw(i) = mean(r2_clean);
    
    rej_all1(i) = 100*(1 - length(r1_clean)/length(r1));
    rej_all2(i) = 100*(1 - length(r2_clean)/length(r2));
end

%% ================== TÍNH TOÁN & HIỆU CHỈNH ==================
% 1. Tính Range thực (Ground Truth)
R1_true = sqrt((X-A1(1)).^2 + (Y-A1(2)).^2 + dz^2);
R2_true = sqrt((X-A2(1)).^2 + (Y-A2(2)).^2 + dz^2);

% 2. Calib Range (Poly2)
p1 = polyfit(R1_raw, R1_true, 2);
p2 = polyfit(R2_raw, R2_true, 2);
R1_cal = polyval(p1, R1_raw);
R2_cal = polyval(p2, R2_raw);

% 3. Chuyển 3D sang 2D mặt phẳng
R1_2D = sqrt(max(R1_cal.^2 - dz^2, 0));
R2_2D = sqrt(max(R2_cal.^2 - dz^2, 0));

% 4. Giải hệ phương trình Trilateration (2 Anchor)
x_raw = zeros(n,1); y_raw = zeros(n,1);
dvec = A2 - A1;
d = norm(dvec);
ex = dvec/d;
ey = [-ex(2), ex(1)];

for i = 1:n
    D = (R1_2D(i)^2 - R2_2D(i)^2 + d^2)/(2*d);
    P = A1 + D*ex;
    h = sqrt(abs(R1_2D(i)^2 - D^2));
    
    % Chọn nghiệm gần với tọa độ thực nhất (vì 2 anchor có 2 nghiệm đối xứng)
    s1 = P + h*ey;
    s2 = P - h*ey;
    if norm(s1 - [X(i),Y(i)]) < norm(s2 - [X(i),Y(i)])
        x_raw(i) = s1(1); y_raw(i) = s1(2);
    else
        x_raw(i) = s2(1); y_raw(i) = s2(2);
    end
end

% 5. Hiệu chỉnh XY (Surface Fitting Poly33)
Fx = fit([x_raw, y_raw], X, 'poly33');
Fy = fit([x_raw, y_raw], Y, 'poly33');
x_fix = Fx(x_raw, y_raw);
y_fix = Fy(x_raw, y_raw);

%% ================== THỐNG KÊ & PLOT ==================
err_raw = sqrt((x_raw-X).^2 + (y_raw-Y).^2);
err_fix = sqrt((x_fix-X).^2 + (y_fix-Y).^2);

fprintf('--- Kết quả sau khi đồng bộ tọa độ file ---\n');
fprintf('Lỗi trung bình TRƯỚC fix: %.4f m\n', mean(err_raw));
fprintf('Lỗi trung bình SAU fix:   %.4f m\n', mean(err_fix));

figure('Name', 'UWB Analysis', 'Color', 'w', 'Position', [100 100 1000 700]);
subplot(2,1,1);
plot(X, Y, 'ko', 'MarkerSize', 8, 'DisplayName', 'Thực tế'); hold on;
plot(x_raw, y_raw, 'r.', 'MarkerSize', 12, 'DisplayName', 'Thô (Raw)');
plot(x_fix, y_fix, 'g*', 'MarkerSize', 8, 'DisplayName', 'Đã sửa (Fixed)');
quiver(x_raw, y_raw, x_fix-x_raw, y_fix-y_raw, 0, 'Color', [0.7 0.7 0.7], 'HandleVisibility', 'off');
title('So sánh vị trí: Thực tế vs Ước lượng');
legend(); grid on; axis equal;

subplot(2,1,2);
bar([err_raw, err_fix]);
legend('Lỗi trước fix', 'Lỗi sau fix');
title('Lỗi khoảng cách tại từng điểm (m)');
grid on;
%% ==========================================================
% IN HỆ SỐ CHO ESP32
%% ==========================================================
fprintf('\n\n');
fprintf('// Copy các hệ số này vào hàm calculatePosition() trên ESP32\n');
fprintf('// ==========================================================\n');

% 1. In hệ số Range Calibration (Poly2)
fprintf('// 1. Range Calibration Coefficients (Poly2)\n');
fprintf('float p1[] = {%.8f, %.8f, %.8f};\n', p1(1), p1(2), p1(3));
fprintf('float p2[] = {%.8f, %.8f, %.8f};\n', p2(1), p2(2), p2(3));
fprintf('\n');

% 2. In hệ số Surface Fitting (Poly33)
% MATLAB Surface Fit (poly33) có dạng: 
% f(x,y) = p00 + p10*x + p01*y + p20*x^2 + p11*x*y + p02*y^2 + p30*x^3 + p21*x^2*y + p12*x*y^2 + p03*y^3
fprintf('// 2. XY Surface Correction (Poly33)\n');
fprintf('// Cấu trúc: {p00, p10, p01, p20, p11, p02, p30, p21, p12, p03}\n');

coeffsX = coeffvalues(Fx);
fprintf('float cx[] = {');
fprintf('%.8f, ', coeffsX(1:end-1)); fprintf('%.8f};\n', coeffsX(end));

coeffsY = coeffvalues(Fy);
fprintf('float cy[] = {');
fprintf('%.8f, ', coeffsY(1:end-1)); fprintf('%.8f};\n', coeffsY(end));

fprintf('// ==========================================================\n');
%% ==========================================================
% 3. TÍNH TOÁN MA TRẬN NHIỄU R CHO EKF (MEASUREMENT NOISE)
%% ==========================================================
% Tính mảng sai số (Residuals) sau khi đã bù trừ hệ thống
err_x = x_fix - X;
err_y = y_fix - Y;

% Tính phương sai (Variance) -> Đây chính là thông số R cho EKF
var_x = var(err_x);
var_y = var(err_y);

% Tính độ lệch chuẩn (Standard Deviation) để hiển thị trực quan (đơn vị cm)
std_x = std(err_x) * 100;
std_y = std(err_y) * 100;

fprintf('\n// ==========================================================\n');
fprintf('// THÔNG SỐ MA TRẬN NHIỄU R CHO EKF (EKF_Sensor_Fusion.h)\n');
fprintf('// ==========================================================\n');
fprintf('/* Dựa trên dữ liệu thực tế, UWB dao động: X = +-%.2f cm, Y = +-%.2f cm */\n', std_x, std_y);
fprintf('#define EKF_R_UWB_X  %.6ff\n', var_x);
fprintf('#define EKF_R_UWB_Y  %.6ff\n', var_y);
fprintf('// ==========================================================\n');

% (Tùy chọn) Vẽ thêm biểu đồ phân phối nhiễu (Histogram) để xem nhiễu có chuẩn (Gaussian) không
figure('Name', 'UWB Noise Distribution', 'Color', 'w', 'Position', [150 150 800 400]);
subplot(1,2,1);
histogram(err_x, 15, 'Normalization', 'pdf', 'FaceColor', 'b', 'FaceAlpha', 0.6);
title(sprintf('Phân phối nhiễu trục X\n(\\sigma = %.2f cm)', std_x));
xlabel('Sai số (m)'); ylabel('Mật độ'); grid on;

subplot(1,2,2);
histogram(err_y, 15, 'Normalization', 'pdf', 'FaceColor', 'r', 'FaceAlpha', 0.6);
title(sprintf('Phân phối nhiễu trục Y\n(\\sigma = %.2f cm)', std_y));
xlabel('Sai số (m)'); grid on;