@tool
extends Window
class_name UniversalCellExpander

# 引入翻译管理器
const TranslationManager = preload("res://addons/json_editor/scripts/translation_manager.gd")

# 信号
signal content_updated(new_content: String)
signal expansion_closed()

# 组件变量
var expansion_header: Panel
var expansion_text_edit: TextEdit
var expansion_table: Control
var expansion_mode_button: Button
var current_expansion_mode: String = "text"
var expansion_data: Variant = null
var original_content: String = ""
var cell_info: Dictionary = {}

# 子展开器管理
var child_expanders: Array[UniversalCellExpander] = []
var parent_expander: UniversalCellExpander = null
var expansion_level: int = 0

# 子窗口来源跟踪
var child_source_mapping: Dictionary = {}  # 存储子窗口与其来源单元格的映射

# 表格数据管理
var table_data: Variant = null
var table_headers: Array = []
var table_cell_inputs: Array = []  # 存储所有单元格的LineEdit引用
var table_grid: GridContainer = null  # 保存表格网格的引用，用于行操作

# 防重复创建标志
var _is_creating_child_expander: bool = false

# 实时输入同步配置
var enable_realtime_sync: bool = true  # 是否启用实时输入同步

func _init():
	# 设置窗口属性
	title = TranslationManager.get_text("content_expansion")
	size = Vector2i(800, 600)  # 增加窗口大小，提供更多空间
	min_size = Vector2i(600, 450)  # 增加最小尺寸
	visible = false
	# 设置窗口模式
	mode = Window.MODE_WINDOWED
	# 允许调整大小
	unresizable = false
	# 设置窗口标志
	always_on_top = false
	# 连接窗口关闭信号
	close_requested.connect(_on_window_close_requested)

	# 注册语言变化监听器
	TranslationManager.add_language_change_listener(_on_language_changed)

func _on_window_close_requested():
	"""处理窗口关闭请求"""
	_close_expansion()

func _on_language_changed(new_language: String):
	"""处理语言变化，更新UI文本"""
	print("浮动窗口语言变化:", new_language)
	_update_ui_translations()

func _update_ui_translations():
	"""更新UI元素的翻译文本"""
	# 更新窗口标题
	if cell_info.has("title"):
		title = cell_info["title"]
	else:
		title = TranslationManager.get_text("content_expansion_level") % (expansion_level + 1)

	# 更新模式切换按钮文本
	if expansion_mode_button:
		if current_expansion_mode == "text":
			expansion_mode_button.text = TranslationManager.get_text("text_mode")
		else:
			expansion_mode_button.text = TranslationManager.get_text("table_mode")

	# 更新文本编辑区域占位符
	if expansion_text_edit:
		expansion_text_edit.placeholder_text = TranslationManager.get_text("content_placeholder")

	# 更新按钮文本
	_update_button_translations()

	# 如果表格已创建，重新创建以更新标题
	if expansion_table and expansion_table.visible and expansion_data != null:
		_create_expansion_table()

	print("浮动窗口UI翻译已更新")

func _update_button_translations():
	"""更新按钮的翻译文本"""
	# 查找并更新取消和确认按钮
	if expansion_header and expansion_header.get_parent():
		var main_vbox = expansion_header.get_parent()
		if main_vbox.get_child_count() >= 3:
			var button_container = main_vbox.get_child(2)
			if button_container and button_container.get_child_count() > 0:
				var button_hbox = button_container.get_child(0)
				if button_hbox and button_hbox.get_child_count() >= 2:
					var cancel_button = button_hbox.get_child(0) as Button
					var confirm_button = button_hbox.get_child(1) as Button
					if cancel_button:
						cancel_button.text = TranslationManager.get_text("cancel_button")
					if confirm_button:
						confirm_button.text = TranslationManager.get_text("confirm_button")

func setup_expansion(content: String, info: Dictionary = {}, level: int = 0):
	"""设置展开内容和信息"""
	original_content = content
	cell_info = info
	expansion_level = level
	
	# 解析内容
	expansion_data = _parse_content(content)
	
	# 创建UI（如果还没有创建）
	if not expansion_header:
		_create_ui()
	
	# 更新标题
	_update_title()
	
	# 设置文本内容
	if expansion_text_edit:
		expansion_text_edit.text = content
	
	# 决定默认模式
	var can_show_table = expansion_data != null and (typeof(expansion_data) in [TYPE_DICTIONARY, TYPE_ARRAY])
	if can_show_table:
		current_expansion_mode = "table"
		expansion_mode_button.text = TranslationManager.get_text("text_mode")
		expansion_mode_button.visible = true
		_show_table_mode()
		print("检测到结构化数据，默认显示表格模式")
	else:
		current_expansion_mode = "text"
		expansion_mode_button.text = TranslationManager.get_text("table_mode")
		expansion_mode_button.visible = false
		_show_text_mode()
		print("无结构化数据，显示文本模式")

func _create_ui():
	"""创建展开区域UI"""
	# 设置窗口背景为白色
	var bg_panel = Panel.new()
	bg_panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var bg_style = StyleBoxFlat.new()
	bg_style.bg_color = Color.WHITE
	bg_panel.add_theme_stylebox_override("panel", bg_style)
	add_child(bg_panel)

	# 创建主容器
	var main_vbox = VBoxContainer.new()
	main_vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	main_vbox.add_theme_constant_override("separation", 0)
	main_vbox.add_theme_constant_override("margin_left", 10)
	main_vbox.add_theme_constant_override("margin_right", 10)
	main_vbox.add_theme_constant_override("margin_top", 10)
	main_vbox.add_theme_constant_override("margin_bottom", 10)
	bg_panel.add_child(main_vbox)

	# 创建标题栏
	expansion_header = Panel.new()
	expansion_header.custom_minimum_size = Vector2(0, 40)  # 增加高度
	main_vbox.add_child(expansion_header)

	# 标题栏样式 - 根据层级调整颜色
	var header_style = StyleBoxFlat.new()
	var header_colors = [
		Color("#4A90E2"),  # 第0级：蓝色
		Color("#28A745"),  # 第1级：绿色
		Color("#FD7E14"),  # 第2级：橙色
		Color("#6F42C1"),  # 第3级：紫色
		Color("#E83E8C"),  # 第4级：粉色
	]
	var color_index = expansion_level % header_colors.size()
	header_style.bg_color = header_colors[color_index]
	header_style.border_width_top = 2
	header_style.border_width_left = 2
	header_style.border_width_right = 2
	header_style.border_color = Color("#FFFFFF")
	header_style.corner_radius_top_left = 8
	header_style.corner_radius_top_right = 8
	# 添加阴影效果
	header_style.shadow_color = Color(0, 0, 0, 0.2)
	header_style.shadow_size = 2
	expansion_header.add_theme_stylebox_override("panel", header_style)
	
	# 标题栏内容
	var header_hbox = HBoxContainer.new()
	header_hbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	header_hbox.add_theme_constant_override("margin_left", 15)
	header_hbox.add_theme_constant_override("margin_right", 15)
	header_hbox.add_theme_constant_override("margin_top", 8)
	header_hbox.add_theme_constant_override("margin_bottom", 8)
	expansion_header.add_child(header_hbox)

	# 层级指示器
	var level_indicator = Label.new()
	level_indicator.text = "L%d" % (expansion_level + 1)
	level_indicator.add_theme_color_override("font_color", Color.WHITE)
	level_indicator.add_theme_font_size_override("font_size", 11)
	level_indicator.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	level_indicator.custom_minimum_size = Vector2(30, 0)
	# 添加背景
	var level_style = StyleBoxFlat.new()
	level_style.bg_color = Color(1, 1, 1, 0.2)
	level_style.corner_radius_top_left = 10
	level_style.corner_radius_top_right = 10
	level_style.corner_radius_bottom_left = 10
	level_style.corner_radius_bottom_right = 10
	level_style.content_margin_left = 6
	level_style.content_margin_right = 6
	level_indicator.add_theme_stylebox_override("normal", level_style)
	header_hbox.add_child(level_indicator)

	# 标题文本
	var title_label = Label.new()
	title_label.text = TranslationManager.get_text("content_expansion")
	title_label.add_theme_color_override("font_color", Color.WHITE)
	title_label.add_theme_font_size_override("font_size", 14)
	title_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	title_label.add_theme_constant_override("margin_left", 10)
	header_hbox.add_child(title_label)

	# 模式切换按钮
	expansion_mode_button = Button.new()
	expansion_mode_button.text = TranslationManager.get_text("table_mode")
	expansion_mode_button.custom_minimum_size = Vector2(70, 28)
	expansion_mode_button.add_theme_color_override("font_color", Color.WHITE)
	expansion_mode_button.add_theme_font_size_override("font_size", 11)
	expansion_mode_button.flat = true
	expansion_mode_button.pressed.connect(_toggle_expansion_mode)
	# 按钮样式
	var btn_style = StyleBoxFlat.new()
	btn_style.bg_color = Color(1, 1, 1, 0.15)
	btn_style.corner_radius_top_left = 4
	btn_style.corner_radius_top_right = 4
	btn_style.corner_radius_bottom_left = 4
	btn_style.corner_radius_bottom_right = 4
	expansion_mode_button.add_theme_stylebox_override("normal", btn_style)
	header_hbox.add_child(expansion_mode_button)

	# 关闭按钮
	var close_button = Button.new()
	close_button.text = "✕"
	close_button.custom_minimum_size = Vector2(32, 28)
	close_button.add_theme_color_override("font_color", Color.WHITE)
	close_button.add_theme_font_size_override("font_size", 16)
	close_button.flat = true
	close_button.pressed.connect(_close_expansion)
	# 关闭按钮样式
	var close_style = StyleBoxFlat.new()
	close_style.bg_color = Color(1, 0, 0, 0.3)
	close_style.corner_radius_top_left = 4
	close_style.corner_radius_top_right = 4
	close_style.corner_radius_bottom_left = 4
	close_style.corner_radius_bottom_right = 4
	close_button.add_theme_stylebox_override("normal", close_style)
	header_hbox.add_child(close_button)
	
	# 创建内容容器
	var content_container = Control.new()
	content_container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content_container.custom_minimum_size = Vector2(0, 400)  # 大幅增加高度，为表格提供更多空间
	main_vbox.add_child(content_container)

	# 创建文本编辑区域
	expansion_text_edit = TextEdit.new()
	expansion_text_edit.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	expansion_text_edit.placeholder_text = TranslationManager.get_text("content_placeholder")

	# 添加实时同步支持
	expansion_text_edit.text_changed.connect(_on_text_edit_changed)

	content_container.add_child(expansion_text_edit)

	# 添加快捷键支持
	expansion_text_edit.gui_input.connect(_on_text_edit_input)

	# 文本编辑区域样式 - 更现代化
	var text_style = StyleBoxFlat.new()
	text_style.bg_color = Color("#FFFFFF")
	text_style.border_width_left = 2
	text_style.border_width_right = 2
	text_style.border_width_bottom = 2
	text_style.border_color = Color("#E1E5E9")
	text_style.corner_radius_bottom_left = 8
	text_style.corner_radius_bottom_right = 8
	text_style.content_margin_left = 15
	text_style.content_margin_right = 15
	text_style.content_margin_top = 15
	text_style.content_margin_bottom = 15
	expansion_text_edit.add_theme_stylebox_override("normal", text_style)

	# 焦点样式
	var text_focus_style = StyleBoxFlat.new()
	text_focus_style.bg_color = Color("#FFFFFF")
	text_focus_style.border_width_left = 2
	text_focus_style.border_width_right = 2
	text_focus_style.border_width_bottom = 2
	text_focus_style.border_color = Color("#4A90E2")
	text_focus_style.corner_radius_bottom_left = 8
	text_focus_style.corner_radius_bottom_right = 8
	text_focus_style.content_margin_left = 15
	text_focus_style.content_margin_right = 15
	text_focus_style.content_margin_top = 15
	text_focus_style.content_margin_bottom = 15
	expansion_text_edit.add_theme_stylebox_override("focus", text_focus_style)

	# 创建表格区域
	expansion_table = Control.new()
	expansion_table.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	expansion_table.visible = false
	content_container.add_child(expansion_table)

	# 表格区域样式 - 白色背景
	var table_style = StyleBoxFlat.new()
	table_style.bg_color = Color.WHITE  # 设置为白色背景
	table_style.border_width_left = 2
	table_style.border_width_right = 2
	table_style.border_width_bottom = 2
	table_style.border_color = Color("#E1E5E9")
	table_style.corner_radius_bottom_left = 8
	table_style.corner_radius_bottom_right = 8
	expansion_table.add_theme_stylebox_override("panel", table_style)
	
	# 创建按钮区域
	var button_container = Panel.new()
	button_container.custom_minimum_size = Vector2(0, 60)  # 增加按钮区域高度
	main_vbox.add_child(button_container)

	# 按钮容器样式
	var button_bg_style = StyleBoxFlat.new()
	button_bg_style.bg_color = Color("#F1F3F4")
	button_bg_style.border_width_left = 2
	button_bg_style.border_width_right = 2
	button_bg_style.border_width_bottom = 2
	button_bg_style.border_color = Color("#E1E5E9")
	button_bg_style.corner_radius_bottom_left = 8
	button_bg_style.corner_radius_bottom_right = 8
	button_container.add_theme_stylebox_override("panel", button_bg_style)

	var button_hbox = HBoxContainer.new()
	button_hbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	button_hbox.alignment = BoxContainer.ALIGNMENT_END
	button_hbox.add_theme_constant_override("margin_top", 15)  # 增加上边距
	button_hbox.add_theme_constant_override("margin_bottom", 15)  # 增加下边距
	button_hbox.add_theme_constant_override("margin_right", 20)  # 增加右边距
	button_hbox.add_theme_constant_override("separation", 15)  # 增加按钮间距
	button_container.add_child(button_hbox)

	# 取消按钮
	var cancel_button = Button.new()
	cancel_button.text = TranslationManager.get_text("cancel_button")
	cancel_button.custom_minimum_size = Vector2(120, 40)  # 增加按钮大小
	cancel_button.pressed.connect(_close_expansion)
	button_hbox.add_child(cancel_button)

	# 确认按钮
	var confirm_button = Button.new()
	confirm_button.text = TranslationManager.get_text("confirm_button")
	confirm_button.custom_minimum_size = Vector2(120, 40)  # 增加按钮大小
	confirm_button.pressed.connect(_apply_content)
	button_hbox.add_child(confirm_button)

	# 设置按钮样式
	_setup_button_styles(cancel_button, confirm_button)

func _update_title():
	"""更新标题显示"""
	# 更新窗口标题
	if cell_info.has("title"):
		title = cell_info["title"]
	else:
		title = TranslationManager.get_text("content_expansion_level") % (expansion_level + 1)

	# 更新内部标题标签
	if expansion_header:
		var header_hbox = expansion_header.get_child(0) as HBoxContainer
		if header_hbox and header_hbox.get_child_count() > 1:
			var title_label = header_hbox.get_child(1) as Label
			if title_label:
				var level_prefix = ""
				for i in range(expansion_level):
					level_prefix += "  "

				if cell_info.has("title"):
					title_label.text = level_prefix + cell_info["title"]
				else:
					title_label.text = level_prefix + TranslationManager.get_text("content_expansion_level") % (expansion_level + 1)

func show_expansion():
	"""显示展开区域"""
	# 根据层级调整窗口大小和位置
	var base_width = 800  # 增加基础宽度
	var base_height = 600  # 增加基础高度
	var level_offset = 40  # 增加层级偏移量

	var window_width = base_width + expansion_level * 60  # 增加宽度偏移
	var window_height = base_height + expansion_level * level_offset

	size = Vector2i(window_width, window_height)
	min_size = Vector2i(600, 450)  # 增加最小尺寸

	# 智能位置管理
	_adjust_position_for_level()

	# 显示窗口
	visible = true

	# 窗口显示动画
	var tween = create_tween()
	tween.tween_property(self, "size", Vector2i(window_width, window_height), 0.3)
	tween.tween_callback(func():
		print("展开动画完成，级别:", expansion_level)
		if current_expansion_mode == "text" and expansion_text_edit:
			expansion_text_edit.grab_focus()
	)

func _adjust_position_for_level():
	"""根据层级调整窗口位置"""
	# 获取主窗口的位置和大小
	var main_window = get_viewport().get_window()
	var main_pos = main_window.position
	var main_size = main_window.size

	# 计算新窗口位置
	var offset_x = expansion_level * 50  # 每级向右偏移50像素
	var offset_y = expansion_level * 40  # 每级向下偏移40像素

	# 设置窗口位置
	position = Vector2i(
		main_pos.x + 100 + offset_x,
		main_pos.y + 100 + offset_y
	)

	print("窗口位置设置为:", position, "级别:", expansion_level)

func _close_expansion():
	"""关闭展开区域"""
	print("关闭展开区域，级别:", expansion_level)

	# 关闭所有子展开器
	_close_all_child_expanders()

	# 移除语言变化监听器
	TranslationManager.remove_language_change_listener(_on_language_changed)

	# 发送关闭信号
	expansion_closed.emit()

	# 添加关闭动画
	var tween = create_tween()
	tween.tween_property(self, "size", Vector2i(100, 100), 0.2)
	tween.tween_callback(func():
		visible = false
		queue_free()  # 释放窗口资源
		print("窗口关闭完成，级别:", expansion_level)
	)

func _apply_content():
	"""应用编辑后的内容"""
	var new_content = ""

	# 根据当前模式获取内容
	if current_expansion_mode == "table" and table_data != null:
		# 表格模式：从表格数据生成JSON
		new_content = JSON.stringify(table_data)
		print("从表格模式获取内容:", new_content)
	elif expansion_text_edit:
		# 文本模式：从文本编辑器获取内容
		new_content = expansion_text_edit.text
		print("从文本模式获取内容:", new_content)

	if new_content != "":
		content_updated.emit(new_content)
		print("内容已更新并发送信号，级别:", expansion_level, "内容:", new_content.substr(0, 100) + "...")
	else:
		print("警告：没有找到要更新的内容")

	_close_expansion()

func _toggle_expansion_mode():
	"""切换展开模式"""
	print("开始切换模式，当前模式:", current_expansion_mode)

	if current_expansion_mode == "text":
		# 从文本模式切换到表格模式
		# 先尝试解析文本内容更新表格数据
		if expansion_text_edit:
			var text_content = expansion_text_edit.text
			var parsed_data = _parse_content(text_content)
			if parsed_data != null:
				expansion_data = parsed_data
				table_data = parsed_data
				print("文本内容已解析并更新到表格数据")
			else:
				print("警告：无法解析文本内容为结构化数据")

		current_expansion_mode = "table"
		expansion_mode_button.text = TranslationManager.get_text("text_mode")
		_show_table_mode()
	else:
		# 从表格模式切换到文本模式
		# 先同步表格数据到文本
		if table_data != null:
			_sync_table_data_back()
			print("表格数据已同步到文本模式")

		current_expansion_mode = "text"
		expansion_mode_button.text = TranslationManager.get_text("table_mode")
		_show_text_mode()

	print("模式切换完成，新模式:", current_expansion_mode, "级别:", expansion_level)

	# 强制更新UI以确保显示正确
	_force_ui_update()

func _force_ui_update():
	"""强制更新UI显示"""
	# 确保正确的组件可见性
	if current_expansion_mode == "text":
		if expansion_text_edit:
			expansion_text_edit.visible = true
		if expansion_table:
			expansion_table.visible = false
	else:
		if expansion_text_edit:
			expansion_text_edit.visible = false
		if expansion_table:
			expansion_table.visible = true

	# 强制更新窗口内容
	print("UI强制更新完成，模式:", current_expansion_mode)

func _show_text_mode():
	"""显示文本模式"""
	if expansion_text_edit:
		expansion_text_edit.visible = true
	if expansion_table:
		expansion_table.visible = false

func _show_table_mode():
	"""显示表格模式"""
	if expansion_text_edit:
		expansion_text_edit.visible = false
	if expansion_table:
		expansion_table.visible = true
		# 重新创建表格以反映最新数据
		_create_expansion_table()

func _close_all_child_expanders():
	"""关闭所有子展开器"""
	for child_expander in child_expanders:
		if child_expander and is_instance_valid(child_expander):
			child_expander._close_expansion()
	child_expanders.clear()

func _parse_content(content: String) -> Variant:
	"""解析内容，尝试转换为结构化数据"""
	print("解析内容，级别:", expansion_level, "内容:", content.substr(0, 50) + "...")

	# 尝试解析为JSON
	var json = JSON.new()
	var parse_result = json.parse(content)

	if parse_result == OK:
		var data = json.data
		print("成功解析为JSON:", typeof(data), "级别:", expansion_level)
		return data
	else:
		print("JSON解析失败，尝试其他格式，级别:", expansion_level)

		# 尝试解析为简单的键值对格式
		if content.contains(":") and content.contains(","):
			var parsed_dict = _parse_key_value_pairs(content)
			if parsed_dict.size() > 0:
				print("成功解析为键值对，级别:", expansion_level)
				return parsed_dict

		# 尝试解析为数组格式
		if content.begins_with("[") and content.ends_with("]"):
			var parsed_array = _parse_simple_array(content)
			if parsed_array.size() > 0:
				print("成功解析为数组，级别:", expansion_level)
				return parsed_array

		# 如果都无法解析，返回null表示解析失败
		print("无法解析为结构化数据，保持原始格式，级别:", expansion_level)
		return null

func _parse_key_value_pairs(content: String) -> Dictionary:
	"""解析简单的键值对格式"""
	var result = {}
	var pairs = content.split(",")

	for pair in pairs:
		if pair.contains(":"):
			var kv = pair.split(":", false, 1)
			if kv.size() == 2:
				var key = kv[0].strip_edges()
				var value = kv[1].strip_edges()
				# 移除引号
				if key.begins_with('"') and key.ends_with('"'):
					key = key.substr(1, key.length() - 2)
				if value.begins_with('"') and value.ends_with('"'):
					value = value.substr(1, value.length() - 2)
				result[key] = value

	return result

func _parse_simple_array(content: String) -> Array:
	"""解析简单的数组格式"""
	var inner = content.substr(1, content.length() - 2)  # 移除 [ ]
	var items = inner.split(",")
	var result = []

	for item in items:
		var trimmed = item.strip_edges()
		# 移除引号
		if trimmed.begins_with('"') and trimmed.ends_with('"'):
			trimmed = trimmed.substr(1, trimmed.length() - 2)
		result.append(trimmed)

	return result

func _create_expansion_table():
	"""创建展开区域的表格"""
	print("创建展开表格，级别:", expansion_level)

	# 清除现有内容
	for child in expansion_table.get_children():
		child.queue_free()

	if expansion_data == null:
		var no_data_label = Label.new()
		no_data_label.text = TranslationManager.get_text("no_table_data")
		no_data_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		no_data_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		no_data_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		expansion_table.add_child(no_data_label)
		return

	# 创建滚动容器
	var scroll = ScrollContainer.new()
	scroll.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scroll.add_theme_constant_override("margin_left", 5)
	scroll.add_theme_constant_override("margin_top", 5)
	scroll.add_theme_constant_override("margin_right", 5)
	scroll.add_theme_constant_override("margin_bottom", 5)
	expansion_table.add_child(scroll)

	# 创建表格网格
	var grid = GridContainer.new()
	grid.add_theme_constant_override("h_separation", 0)
	grid.add_theme_constant_override("v_separation", 0)
	scroll.add_child(grid)

	# 保存网格引用用于行操作
	table_grid = grid

	# 根据数据类型创建不同的表格
	match typeof(expansion_data):
		TYPE_DICTIONARY:
			_create_dictionary_table(grid, expansion_data)
		TYPE_ARRAY:
			_create_array_table(grid, expansion_data)
		_:
			_create_simple_table(grid, expansion_data)

func _create_dictionary_table(grid: GridContainer, data: Dictionary):
	"""为字典数据创建表格"""
	print("创建字典表格，键数量:", data.size(), "级别:", expansion_level)

	grid.columns = 3  # 行号 + 键 + 值
	table_headers = [TranslationManager.get_text("row_number_header"), TranslationManager.get_text("table_header_key"), TranslationManager.get_text("table_header_value")]
	table_data = data
	table_cell_inputs = []

	# 创建标题行
	var row_header = _create_header_cell(TranslationManager.get_text("row_number_header"))
	var key_header = _create_header_cell(TranslationManager.get_text("table_header_key"))
	var value_header = _create_header_cell(TranslationManager.get_text("table_header_value"))
	grid.add_child(row_header)
	grid.add_child(key_header)
	grid.add_child(value_header)

	# 创建数据行
	var row_index = 0
	for key in data.keys():
		var value = data[key]
		var row_cells = []

		# 创建行号单元格
		var row_number_cell = _create_row_number_cell(row_index + 1)
		row_number_cell.gui_input.connect(_on_row_number_cell_input.bind(row_index))

		var key_cell = _create_data_cell(str(key), false)
		var value_cell = _create_data_cell(str(value), _is_expandable_content(str(value)))

		# 连接单元格事件 (注意列索引偏移，因为添加了行号列)
		key_cell.text_submitted.connect(_on_table_cell_text_submitted.bind(row_index, 1))
		value_cell.text_submitted.connect(_on_table_cell_text_submitted.bind(row_index, 2))
		# 添加实时输入同步
		key_cell.text_changed.connect(_on_table_cell_text_changed.bind(row_index, 1))
		value_cell.text_changed.connect(_on_table_cell_text_changed.bind(row_index, 2))
		key_cell.gui_input.connect(_on_table_cell_input.bind(row_index, 1, key_cell))
		value_cell.gui_input.connect(_on_table_cell_input.bind(row_index, 2, value_cell))

		row_cells.append(row_number_cell)
		row_cells.append(key_cell)
		row_cells.append(value_cell)
		table_cell_inputs.append(row_cells)

		grid.add_child(row_number_cell)
		grid.add_child(key_cell)
		grid.add_child(value_cell)
		row_index += 1

func _create_array_table(grid: GridContainer, data: Array):
	"""为数组数据创建表格"""
	print("创建数组表格，元素数量:", data.size(), "级别:", expansion_level)

	if data.is_empty():
		grid.columns = 1
		var empty_label = _create_data_cell(TranslationManager.get_text("array_empty"), false)
		grid.add_child(empty_label)
		return

	# 检查是否为对象数组
	var first_item = data[0]
	if typeof(first_item) == TYPE_DICTIONARY:
		_create_object_array_table(grid, data)
	else:
		_create_simple_array_table(grid, data)

func _create_object_array_table(grid: GridContainer, data: Array):
	"""为对象数组创建表格"""
	print("创建对象数组表格，级别:", expansion_level)

	# 收集所有可能的键
	var all_keys = {}
	for item in data:
		if typeof(item) == TYPE_DICTIONARY:
			for key in item.keys():
				all_keys[key] = true

	var keys_array = all_keys.keys()
	keys_array.sort()

	grid.columns = keys_array.size() + 1  # 添加行号列
	table_headers = [TranslationManager.get_text("row_number_header")] + keys_array
	table_data = data
	table_cell_inputs = []

	# 创建标题行
	var row_header = _create_header_cell(TranslationManager.get_text("row_number_header"))
	grid.add_child(row_header)
	for key in keys_array:
		var header_cell = _create_header_cell(str(key))
		grid.add_child(header_cell)

	# 创建数据行
	var row_index = 0
	for item in data:
		if typeof(item) == TYPE_DICTIONARY:
			var row_cells = []
			var col_index = 0

			# 创建行号单元格
			var row_number_cell = _create_row_number_cell(row_index + 1)
			row_number_cell.gui_input.connect(_on_row_number_cell_input.bind(row_index))
			row_cells.append(row_number_cell)
			grid.add_child(row_number_cell)
			col_index += 1

			for key in keys_array:
				var value = ""
				if item.has(key):
					value = str(item[key])
				var data_cell = _create_data_cell(value, _is_expandable_content(value))

				# 连接单元格事件 (注意列索引偏移，因为添加了行号列)
				data_cell.text_submitted.connect(_on_table_cell_text_submitted.bind(row_index, col_index))
				# 添加实时输入同步
				data_cell.text_changed.connect(_on_table_cell_text_changed.bind(row_index, col_index))
				data_cell.gui_input.connect(_on_table_cell_input.bind(row_index, col_index, data_cell))

				row_cells.append(data_cell)
				grid.add_child(data_cell)
				col_index += 1

			table_cell_inputs.append(row_cells)
			row_index += 1

func _create_simple_array_table(grid: GridContainer, data: Array):
	"""为简单数组创建表格"""
	print("创建简单数组表格，级别:", expansion_level)

	grid.columns = 3  # 行号 + 索引 + 值
	table_headers = [TranslationManager.get_text("row_number_header"), TranslationManager.get_text("table_header_index"), TranslationManager.get_text("table_header_value")]
	table_data = data
	table_cell_inputs = []

	# 创建标题行
	var row_header = _create_header_cell(TranslationManager.get_text("row_number_header"))
	var index_header = _create_header_cell(TranslationManager.get_text("table_header_index"))
	var value_header = _create_header_cell(TranslationManager.get_text("table_header_value"))
	grid.add_child(row_header)
	grid.add_child(index_header)
	grid.add_child(value_header)

	# 创建数据行
	for i in range(data.size()):
		var row_cells = []

		# 创建行号单元格
		var row_number_cell = _create_row_number_cell(i + 1)
		row_number_cell.gui_input.connect(_on_row_number_cell_input.bind(i))

		var index_cell = _create_data_cell(str(i), false)
		var value_str = str(data[i])
		var value_cell = _create_data_cell(value_str, _is_expandable_content(value_str))

		# 连接单元格事件 (注意列索引偏移，因为添加了行号列)
		index_cell.text_submitted.connect(_on_table_cell_text_submitted.bind(i, 1))
		value_cell.text_submitted.connect(_on_table_cell_text_submitted.bind(i, 2))
		# 添加实时输入同步
		index_cell.text_changed.connect(_on_table_cell_text_changed.bind(i, 1))
		value_cell.text_changed.connect(_on_table_cell_text_changed.bind(i, 2))
		index_cell.gui_input.connect(_on_table_cell_input.bind(i, 1, index_cell))
		value_cell.gui_input.connect(_on_table_cell_input.bind(i, 2, value_cell))

		row_cells.append(row_number_cell)
		row_cells.append(index_cell)
		row_cells.append(value_cell)
		table_cell_inputs.append(row_cells)

		grid.add_child(row_number_cell)
		grid.add_child(index_cell)
		grid.add_child(value_cell)

func _create_simple_table(grid: GridContainer, data: Variant):
	"""为简单数据创建表格"""
	print("创建简单数据表格，级别:", expansion_level)

	grid.columns = 1

	var header_cell = _create_header_cell(TranslationManager.get_text("table_header_value"))
	grid.add_child(header_cell)

	var data_str = str(data)
	var data_cell = _create_data_cell(data_str, _is_expandable_content(data_str))
	grid.add_child(data_cell)

func _is_expandable_content(content: String) -> bool:
	"""检查内容是否可以展开（使用静态方法的逻辑）"""
	return UniversalCellExpander.is_content_expandable(content)

func _create_header_cell(text: String) -> Panel:
	"""创建表格标题单元格"""
	var panel = Panel.new()
	panel.custom_minimum_size = Vector2(100, 30)

	var style = StyleBoxFlat.new()
	style.bg_color = Color("#4A90E2")
	style.border_width_bottom = 1
	style.border_width_right = 1
	style.border_width_top = 1
	style.border_width_left = 1
	style.border_color = Color("#FFFFFF")
	panel.add_theme_stylebox_override("panel", style)

	var label = Label.new()
	label.text = text
	label.add_theme_color_override("font_color", Color.WHITE)
	label.add_theme_font_size_override("font_size", 12)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(label)

	return panel

func _create_row_number_cell(row_number: int) -> Panel:
	"""创建行号单元格"""
	var panel = Panel.new()
	panel.custom_minimum_size = Vector2(50, 30)

	# 设置行号样式 - 类似主表格的行号样式
	var style = StyleBoxFlat.new()
	style.bg_color = Color("#2E3440")  # 更深的灰色
	style.border_width_bottom = 1
	style.border_width_right = 1
	style.border_width_top = 1
	style.border_width_left = 1
	style.border_color = Color("#FFFFFF")
	panel.add_theme_stylebox_override("panel", style)

	var label = Label.new()
	label.text = str(row_number)
	label.add_theme_color_override("font_color", Color.WHITE)
	label.add_theme_font_size_override("font_size", 12)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	panel.add_child(label)

	return panel

func _create_data_cell(text: String, expandable: bool = false) -> LineEdit:
	"""创建表格数据单元格 - 可编辑的LineEdit"""
	var line_edit = LineEdit.new()
	line_edit.text = text
	line_edit.custom_minimum_size = Vector2(120, 30)
	line_edit.placeholder_text = TranslationManager.get_text("input_value_placeholder")

	# 普通状态样式
	var normal_style = StyleBoxFlat.new()
	normal_style.bg_color = Color.WHITE
	normal_style.border_width_bottom = 1
	normal_style.border_width_right = 1
	normal_style.border_width_top = 1
	normal_style.border_width_left = 1
	normal_style.border_color = Color("#E1E5E9")
	normal_style.content_margin_left = 8
	normal_style.content_margin_right = 8
	normal_style.content_margin_top = 4
	normal_style.content_margin_bottom = 4
	line_edit.add_theme_stylebox_override("normal", normal_style)

	# 焦点状态样式
	var focus_style = StyleBoxFlat.new()
	focus_style.bg_color = Color("#FFFFFF")
	focus_style.border_width_left = 2
	focus_style.border_width_top = 2
	focus_style.border_width_right = 2
	focus_style.border_width_bottom = 2
	focus_style.border_color = Color("#4A90E2")
	focus_style.content_margin_left = 8
	focus_style.content_margin_right = 8
	focus_style.content_margin_top = 4
	focus_style.content_margin_bottom = 4
	line_edit.add_theme_stylebox_override("focus", focus_style)

	# 字体样式
	line_edit.add_theme_font_size_override("font_size", 12)
	line_edit.add_theme_color_override("font_color", Color("#333333"))
	line_edit.add_theme_color_override("font_placeholder_color", Color("#999999"))

	# 如果文本太长，添加省略号到tooltip
	if text.length() > 50:
		line_edit.tooltip_text = text

	# 如果内容可以展开，添加视觉提示（但不在这里添加双击事件，避免重复）
	if expandable:
		# 可展开单元格的特殊样式
		var expandable_style = StyleBoxFlat.new()
		expandable_style.bg_color = Color("#E3F2FD")  # 浅蓝色背景
		expandable_style.border_color = Color("#2196F3")
		expandable_style.border_width_left = 2
		expandable_style.border_width_right = 2
		expandable_style.border_width_top = 2
		expandable_style.border_width_bottom = 2
		expandable_style.content_margin_left = 8
		expandable_style.content_margin_right = 8
		expandable_style.content_margin_top = 4
		expandable_style.content_margin_bottom = 4
		line_edit.add_theme_stylebox_override("normal", expandable_style)

		line_edit.add_theme_color_override("font_color", Color("#1976D2"))
		line_edit.tooltip_text = (line_edit.tooltip_text + "\n" if line_edit.tooltip_text else "") + TranslationManager.get_text("double_click_expand_tooltip")

		# 添加悬停效果
		var hover_style = StyleBoxFlat.new()
		hover_style.bg_color = Color("#BBDEFB")  # 更深的蓝色
		hover_style.border_width_bottom = 2
		hover_style.border_width_right = 2
		hover_style.border_width_top = 2
		hover_style.border_width_left = 2
		hover_style.border_color = Color("#1976D2")
		hover_style.content_margin_left = 8
		hover_style.content_margin_right = 8
		hover_style.content_margin_top = 4
		hover_style.content_margin_bottom = 4

		line_edit.mouse_entered.connect(func():
			line_edit.add_theme_stylebox_override("normal", hover_style)
		)
		line_edit.mouse_exited.connect(func():
			line_edit.add_theme_stylebox_override("normal", expandable_style)
		)

	return line_edit

# 已删除 _on_expandable_cell_input 函数，避免重复事件处理

func _create_child_expander(content: String):
	"""创建子展开器（兼容性函数）"""
	_create_child_expander_with_source(content, -1, -1, null)

func _create_child_expander_with_source(content: String, source_row: int, source_col: int, source_cell: LineEdit):
	"""创建子展开器并记录来源信息"""
	print(TranslationManager.get_text("child_expander_created"), ", ", TranslationManager.get_text("content_expansion_level") % (expansion_level + 1), " 来源位置: 行", source_row, " 列", source_col)

	# 限制最大展开层级，避免界面过于复杂
	if expansion_level >= 4:
		print(TranslationManager.get_text("max_expansion_level_reached"))
		return

	# 创建新的展开器窗口
	var child_expander = UniversalCellExpander.new()
	child_expander.parent_expander = self
	child_expanders.append(child_expander)

	# 记录子窗口的来源信息
	var source_info = {
		"row": source_row,
		"col": source_col,
		"cell": source_cell,
		"original_content": content
	}
	child_source_mapping[child_expander] = source_info
	print(TranslationManager.get_text("source_info_recorded"), ":", source_info)

	# 添加到场景树（作为独立窗口）
	get_tree().root.add_child(child_expander)

	# 设置展开信息
	var level_icons = ["🔍", "🔎", "🔬", "🔭", "🎯"]
	var icon = level_icons[min(expansion_level + 1, level_icons.size() - 1)]
	var child_info = {
		"title": TranslationManager.get_text("level_expansion") % [icon, expansion_level + 2]
	}

	child_expander.setup_expansion(content, child_info, expansion_level + 1)
	child_expander.show_expansion()

	# 连接信号
	child_expander.content_updated.connect(func(new_content: String): _on_child_content_updated_with_source(child_expander, new_content))
	child_expander.expansion_closed.connect(_on_child_expansion_closed.bind(child_expander))

func _on_child_content_updated_with_source(child_expander: UniversalCellExpander, new_content: String):
	"""处理子展开器的内容更新（带来源信息）"""
	print("=== 子展开器内容更新开始处理，当前级别:", expansion_level, " ===")
	print("接收到的新内容:", new_content.substr(0, 100) + "...")

	# 获取子窗口的来源信息
	if child_source_mapping.has(child_expander):
		var source_info = child_source_mapping[child_expander]
		print(TranslationManager.get_text("source_info_found"), ":", source_info)

		# 更新对应的单元格
		_update_specific_cell_with_content(source_info, new_content)
	else:
		print(TranslationManager.get_text("source_info_not_found"))
		# 回退到通用更新方法
		_update_current_window_with_child_content(new_content)

	# 向上传播：如果当前窗口也有父窗口，继续向上传播
	if parent_expander != null:
		print("向父级窗口传播更新，目标级别:", expansion_level - 1)
		# 获取当前窗口的完整内容
		var current_content = _get_current_window_content()
		# 向父窗口发送更新信号
		content_updated.emit(current_content)
	else:
		print("已到达顶级窗口，向主表格传播更新")
		# 如果是顶级窗口，向主表格发送更新
		var current_content = _get_current_window_content()
		content_updated.emit(current_content)

	print("=== 子展开器内容更新处理完成 ===")

func _on_child_content_updated(new_content: String):
	"""处理子展开器的内容更新（兼容性函数）"""
	print("使用兼容性函数处理子内容更新")
	_update_current_window_with_child_content(new_content)

func _get_current_window_content() -> String:
	"""获取当前窗口的完整内容"""
	if current_expansion_mode == "table" and table_data != null:
		# 表格模式：从表格数据生成JSON
		return JSON.stringify(table_data)
	elif expansion_text_edit:
		# 文本模式：从文本编辑器获取内容
		return expansion_text_edit.text
	else:
		# 回退到原始内容
		return original_content

func _update_specific_cell_with_content(source_info: Dictionary, new_content: String):
	"""用新内容更新特定的单元格"""
	print("更新特定单元格，行:", source_info.get("row", -1), " 列:", source_info.get("col", -1))

	var row = source_info.get("row", -1)
	var col = source_info.get("col", -1)
	var source_cell = source_info.get("cell", null)

	if row >= 0 and col >= 0:
		# 更新表格数据
		if current_expansion_mode == "table" and table_data != null:
			print("在表格模式中更新数据")
			_update_table_cell_value(row, col, new_content)

		# 更新UI单元格
		if source_cell and is_instance_valid(source_cell):
			print("更新UI单元格显示")
			source_cell.text = new_content
		elif row < table_cell_inputs.size() and col < table_cell_inputs[row].size():
			print("通过索引更新UI单元格")
			table_cell_inputs[row][col].text = new_content

		# 同步到文本模式
		if current_expansion_mode == "table":
			_sync_table_data_back()
	else:
		print("无效的单元格位置，使用通用更新方法")
		_update_current_window_with_child_content(new_content)

func _update_current_window_with_child_content(new_content: String):
	"""用子窗口的新内容更新当前窗口"""
	print("更新当前窗口内容，级别:", expansion_level)

	# 这个函数需要找到子内容在当前窗口中的位置并更新
	# 由于我们无法直接知道子内容对应当前窗口的哪个部分，
	# 我们需要一个更智能的方法来处理这个问题

	# 临时解决方案：如果当前是表格模式，尝试重新解析整个内容
	if current_expansion_mode == "table" and table_data != null:
		print("表格模式：需要找到并更新对应的单元格")
		# 这里需要更复杂的逻辑来定位和更新特定的单元格
		# 暂时先同步到文本模式
		_sync_table_data_back()
	elif expansion_text_edit:
		print("文本模式：内容已在文本编辑器中")
		# 文本模式下，内容已经在文本编辑器中，无需特殊处理

	print("当前窗口内容更新完成")

func _on_child_expansion_closed(child_expander: UniversalCellExpander):
	"""处理子展开器关闭"""
	print(TranslationManager.get_text("child_expander_closed"), ", ", TranslationManager.get_text("content_expansion_level") % expansion_level)

	# 清理来源映射
	if child_source_mapping.has(child_expander):
		print(TranslationManager.get_text("cleanup_source_mapping"))
		child_source_mapping.erase(child_expander)

	child_expanders.erase(child_expander)
	if child_expander and is_instance_valid(child_expander):
		child_expander.queue_free()

func _on_table_cell_text_changed(text: String, row: int, col: int):
	"""处理表格单元格文本实时变化"""
	if enable_realtime_sync:
		print(TranslationManager.get_text("floating_window_realtime_input"), ": 行", row, " 列", col, " 内容:", text.substr(0, 30) + "...")
		# 立即更新表格数据
		_update_table_cell_value(row, col, text)

		# 实时同步回文本编辑器
		_sync_table_data_back()

		# 发出内容更新信号，实现真正的实时同步
		var updated_content = expansion_text_edit.text if expansion_text_edit else ""
		content_updated.emit(updated_content)

func _on_text_edit_changed():
	"""处理文本编辑器实时变化"""
	if enable_realtime_sync and expansion_text_edit:
		print(TranslationManager.get_text("floating_window_text_realtime_input"), ": ", expansion_text_edit.text.substr(0, 50) + "...")

		# 尝试解析文本内容并更新表格（如果在表格模式）
		if current_expansion_mode == "table":
			var parsed_data = _parse_content(expansion_text_edit.text)
			if parsed_data != null:
				expansion_data = parsed_data
				table_data = parsed_data
				# 重新创建表格以反映更改
				_create_expansion_table()

		# 发出实时内容更新信号
		content_updated.emit(expansion_text_edit.text)

func _on_row_number_cell_input(event: InputEvent, row_index: int):
	"""处理行号单元格输入事件（右键菜单）"""
	if event is InputEventMouseButton:
		var mouse_event = event as InputEventMouseButton
		if mouse_event.pressed and mouse_event.button_index == MOUSE_BUTTON_RIGHT:
			print("行号右键点击，行:", row_index)
			# 获取全局鼠标位置
			var global_position = get_viewport().get_mouse_position()
			_show_row_context_menu(row_index, global_position)

# ===== 行操作功能 =====
func _show_row_context_menu(row_index: int, position: Vector2):
	"""显示行操作的右键菜单"""
	var popup = PopupMenu.new()
	popup.add_item(TranslationManager.get_text("add_new_row_before"), 0)
	popup.add_item(TranslationManager.get_text("add_new_row_after"), 1)
	popup.add_separator()
	popup.add_item(TranslationManager.get_text("copy_this_row"), 2)
	popup.add_separator()
	popup.add_item(TranslationManager.get_text("delete_this_row"), 3)

	popup.id_pressed.connect(func(id): _on_row_context_menu_selected(id, row_index))
	get_viewport().add_child(popup)
	popup.position = Vector2i(position)
	popup.popup()

	# 自动清理
	popup.popup_hide.connect(func(): popup.queue_free())

func _on_row_context_menu_selected(id: int, row_index: int):
	"""处理行操作菜单选择"""
	print("浮动窗口菜单选择 - ID: ", id, ", 行号: ", row_index)
	match id:
		0: # 在此行之前添加
			print("执行：在此行之前添加")
			_add_row(row_index)
		1: # 在此行之后添加
			print("执行：在此行之后添加")
			_add_row(row_index + 1)
		2: # 复制此行
			print("执行：复制此行")
			_copy_row(row_index)
		3: # 删除此行
			print("执行：删除此行")
			_delete_row(row_index)

func _add_row(row_index: int):
	"""添加新行"""
	print("浮动窗口添加行到索引: ", row_index)

	var data_type = _get_data_type()
	match data_type:
		"dictionary":
			_add_row_to_dictionary(row_index)
		"simple_array":
			_add_row_to_simple_array(row_index)
		"object_array":
			_add_row_to_object_array(row_index)

func _copy_row(row_index: int):
	"""复制行"""
	print("浮动窗口复制行索引: ", row_index)

	var data_type = _get_data_type()
	match data_type:
		"dictionary":
			_copy_row_in_dictionary(row_index)
		"simple_array":
			_copy_row_in_simple_array(row_index)
		"object_array":
			_copy_row_in_object_array(row_index)

func _delete_row(row_index: int):
	"""删除行"""
	print("浮动窗口删除行索引: ", row_index)

	# 确认对话框
	var dialog = ConfirmationDialog.new()
	dialog.dialog_text = TranslationManager.get_text("confirm_delete_row") % (row_index + 1)
	dialog.title = TranslationManager.get_text("confirm_delete")

	# 设置按钮文本
	dialog.ok_button_text = TranslationManager.get_text("confirm")
	dialog.cancel_button_text = TranslationManager.get_text("cancel")

	get_viewport().add_child(dialog)
	dialog.confirmed.connect(_confirm_delete_row.bind(row_index))
	dialog.canceled.connect(func(): dialog.queue_free())
	dialog.popup_centered()

	# 自动清理
	dialog.confirmed.connect(func(): dialog.queue_free(), CONNECT_ONE_SHOT)
	dialog.canceled.connect(func(): dialog.queue_free(), CONNECT_ONE_SHOT)

func _confirm_delete_row(row_index: int):
	"""确认删除行"""
	var data_type = _get_data_type()
	match data_type:
		"dictionary":
			_delete_row_from_dictionary(row_index)
		"simple_array":
			_delete_row_from_simple_array(row_index)
		"object_array":
			_delete_row_from_object_array(row_index)

func _get_data_type() -> String:
	"""获取数据类型"""
	if typeof(expansion_data) == TYPE_DICTIONARY:
		return "dictionary"
	elif typeof(expansion_data) == TYPE_ARRAY:
		if expansion_data.size() > 0 and typeof(expansion_data[0]) == TYPE_DICTIONARY:
			return "object_array"
		else:
			return "simple_array"
	else:
		return "simple"

# ===== 字典数据行操作 =====
func _add_row_to_dictionary(row_index: int):
	"""为字典数据添加新行"""
	print("字典模式 - 添加行到索引: ", row_index)

	var new_key = _generate_unique_key()
	var new_value = TranslationManager.get_text("new_value_default")

	# 创建新的有序字典
	var new_data = {}
	var keys_array = expansion_data.keys()

	# 在指定位置插入新键值对
	var inserted = false
	for i in range(keys_array.size()):
		if i == row_index and not inserted:
			new_data[new_key] = new_value
			inserted = true

		var key = keys_array[i]
		new_data[key] = expansion_data[key]

	# 如果还没有插入，则添加到末尾
	if not inserted:
		new_data[new_key] = new_value

	expansion_data = new_data
	_rebuild_table_and_sync()

func _copy_row_in_dictionary(row_index: int):
	"""复制字典中的行"""
	print("字典模式 - 复制行索引: ", row_index)

	var keys_array = expansion_data.keys()
	if row_index >= keys_array.size():
		return

	var original_key = keys_array[row_index]
	var original_value = expansion_data[original_key]
	var new_key = _generate_unique_key()

	expansion_data[new_key] = original_value
	_rebuild_table_and_sync()

func _delete_row_from_dictionary(row_index: int):
	"""删除字典中的行"""
	print("字典模式 - 删除行索引: ", row_index)

	var keys_array = expansion_data.keys()
	if row_index >= keys_array.size():
		return

	var key_to_delete = keys_array[row_index]
	expansion_data.erase(key_to_delete)
	_rebuild_table_and_sync()

# ===== 简单数组行操作 =====
func _add_row_to_simple_array(row_index: int):
	"""为简单数组添加新行"""
	print("简单数组模式 - 添加行到索引: ", row_index)

	var new_value = TranslationManager.get_text("new_value_default")

	if row_index >= expansion_data.size():
		expansion_data.append(new_value)
	else:
		expansion_data.insert(row_index, new_value)

	_rebuild_table_and_sync()

func _copy_row_in_simple_array(row_index: int):
	"""复制简单数组中的行"""
	print("简单数组模式 - 复制行索引: ", row_index)

	if row_index >= expansion_data.size():
		return

	var value_to_copy = expansion_data[row_index]
	expansion_data.insert(row_index + 1, value_to_copy)
	_rebuild_table_and_sync()

func _delete_row_from_simple_array(row_index: int):
	"""删除简单数组中的行"""
	print("简单数组模式 - 删除行索引: ", row_index)

	if row_index >= expansion_data.size():
		return

	expansion_data.remove_at(row_index)
	_rebuild_table_and_sync()

# ===== 对象数组行操作 =====
func _add_row_to_object_array(row_index: int):
	"""为对象数组添加新行"""
	print("对象数组模式 - 添加行到索引: ", row_index)

	var new_obj = {}

	# 为新对象创建默认值
	for header in table_headers:
		if header != TranslationManager.get_text("row_number_header"):  # 跳过行号列
			new_obj[header] = TranslationManager.get_text("new_value_default")

	if row_index >= expansion_data.size():
		expansion_data.append(new_obj)
	else:
		expansion_data.insert(row_index, new_obj)

	_rebuild_table_and_sync()

func _copy_row_in_object_array(row_index: int):
	"""复制对象数组中的行"""
	print("对象数组模式 - 复制行索引: ", row_index)

	if row_index >= expansion_data.size():
		return

	var obj_to_copy = expansion_data[row_index]
	if typeof(obj_to_copy) == TYPE_DICTIONARY:
		var new_obj = obj_to_copy.duplicate(true)
		expansion_data.insert(row_index + 1, new_obj)
		_rebuild_table_and_sync()

func _delete_row_from_object_array(row_index: int):
	"""删除对象数组中的行"""
	print("对象数组模式 - 删除行索引: ", row_index)

	if row_index >= expansion_data.size():
		return

	expansion_data.remove_at(row_index)
	_rebuild_table_and_sync()

# ===== 辅助函数 =====
func _generate_unique_key() -> String:
	"""生成唯一键名"""
	var base_name = "new_key"
	var counter = 1

	while expansion_data.has(base_name + "_" + str(counter)):
		counter += 1

	return base_name + "_" + str(counter)

func _rebuild_table_and_sync():
	"""重建表格并同步数据"""
	print("重建浮动窗口表格并同步数据")

	# 重建表格
	_create_expansion_table()

	# 同步数据回文本编辑器
	if expansion_text_edit:
		expansion_text_edit.text = JSON.stringify(expansion_data, "\t")

	# 发出内容更新信号
	content_updated.emit(JSON.stringify(expansion_data, "\t"))

# 实时同步配置接口
func enable_realtime_sync_mode(enabled: bool):
	"""启用或禁用浮动窗口的实时输入同步"""
	enable_realtime_sync = enabled
	print(TranslationManager.get_text("realtime_input_sync_enabled") if enabled else TranslationManager.get_text("realtime_input_sync_disabled"))

func _on_table_cell_text_submitted(text: String, row: int, col: int):
	"""处理表格单元格文本提交"""
	print("表格单元格文本提交，行:", row, " 列:", col, " 值:", text)
	_update_table_cell_value(row, col, text)
	_move_to_next_table_cell(row, col)

func _on_table_cell_input(event: InputEvent, row: int, col: int, line_edit: LineEdit):
	"""处理表格单元格输入事件"""
	# 处理双击展开功能
	if event is InputEventMouseButton:
		var mouse_event = event as InputEventMouseButton
		if mouse_event.pressed and mouse_event.button_index == MOUSE_BUTTON_LEFT:
			if mouse_event.double_click:
				print("检测到表格单元格双击事件，行:", row, " 列:", col)
				var content = line_edit.text
				if _is_expandable_content(content):
					print("表格单元格内容适合展开，级别:", expansion_level)
					line_edit.release_focus()

					# 防止重复创建：检查是否已经在处理展开
					if not _is_creating_child_expander:
						_is_creating_child_expander = true
						# 创建子展开器时传递来源信息
						_create_child_expander_with_source(content, row, col, line_edit)
						# 延迟重置标志，防止快速重复点击
						get_tree().create_timer(0.5).timeout.connect(func(): _is_creating_child_expander = false)

					get_viewport().set_input_as_handled()
					return

	# 处理键盘导航
	if event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_TAB:
				_update_table_cell_value(row, col, line_edit.text)
				if event.shift_pressed:
					_move_to_previous_table_cell(row, col)
				else:
					_move_to_next_table_cell(row, col)
				get_viewport().set_input_as_handled()
			KEY_ENTER:
				_update_table_cell_value(row, col, line_edit.text)
				_move_to_next_table_row(row, col)
				get_viewport().set_input_as_handled()

func _update_table_cell_value(row: int, col: int, value: String):
	"""更新表格单元格的值"""
	if not table_data:
		return

	print("更新表格单元格值，行:", row, " 列:", col, " 新值:", value)

	# 跳过行号列（列0），只处理数据列
	if col == 0:
		print("跳过行号列更新")
		return

	# 根据数据类型更新
	match typeof(table_data):
		TYPE_DICTIONARY:
			_update_dictionary_cell(row, col, value)
		TYPE_ARRAY:
			_update_array_cell(row, col, value)

	# 更新原始内容
	_sync_table_data_back()

func _update_dictionary_cell(row: int, col: int, value: String):
	"""更新字典数据的单元格"""
	var keys = table_data.keys()
	if row < keys.size():
		var key = keys[row]
		if col == 1:
			# 更新键 (列1，因为列0是行号)
			var old_value = table_data[key]
			table_data.erase(key)
			table_data[value] = old_value
		elif col == 2:
			# 更新值 (列2，因为列0是行号)
			table_data[key] = value

func _update_array_cell(row: int, col: int, value: String):
	"""更新数组数据的单元格"""
	if row < table_data.size():
		if typeof(table_data[row]) == TYPE_DICTIONARY:
			# 对象数组 (列0是行号，从列1开始是数据)
			if col < table_headers.size():
				var field_name = table_headers[col]
				table_data[row][field_name] = value
		else:
			# 简单数组 (列0是行号，列1是索引，列2是值)
			if col == 2:  # 值列
				table_data[row] = value

func _sync_table_data_back():
	"""同步表格数据回原始内容"""
	if table_data:
		var json_string = JSON.stringify(table_data)
		if expansion_text_edit:
			expansion_text_edit.text = json_string
		print("表格数据已同步到文本模式:", json_string.substr(0, 100) + "...")

		# 更新原始内容，确保数据一致性
		original_content = json_string

func _move_to_next_table_cell(row: int, col: int):
	"""移动到下一个表格单元格"""
	var next_col = col + 1
	var next_row = row

	if next_col >= table_headers.size():
		next_col = 0
		next_row += 1

	_focus_table_cell(next_row, next_col)

func _move_to_previous_table_cell(row: int, col: int):
	"""移动到上一个表格单元格"""
	var prev_col = col - 1
	var prev_row = row

	if prev_col < 0:
		prev_col = table_headers.size() - 1
		prev_row -= 1

	_focus_table_cell(prev_row, prev_col)

func _move_to_next_table_row(row: int, col: int):
	"""移动到下一行的同一列"""
	_focus_table_cell(row + 1, col)

func _focus_table_cell(row: int, col: int):
	"""聚焦到指定的表格单元格"""
	if row >= 0 and row < table_cell_inputs.size() and col >= 0 and col < table_cell_inputs[row].size():
		table_cell_inputs[row][col].grab_focus()

func _on_text_edit_input(event: InputEvent):
	"""处理文本编辑器的输入事件"""
	if event is InputEventKey and event.pressed:
		# Ctrl+Enter 或 Cmd+Enter 保存内容
		if event.keycode == KEY_ENTER and (event.ctrl_pressed or event.meta_pressed):
			print("检测到快捷键保存：Ctrl/Cmd+Enter")
			_apply_content()
			get_viewport().set_input_as_handled()

func _setup_button_styles(cancel_button: Button, confirm_button: Button):
	"""设置按钮样式"""
	# 取消按钮样式
	var cancel_normal = StyleBoxFlat.new()
	cancel_normal.bg_color = Color("#F8F9FA")
	cancel_normal.border_width_left = 2
	cancel_normal.border_width_top = 2
	cancel_normal.border_width_right = 2
	cancel_normal.border_width_bottom = 2
	cancel_normal.border_color = Color("#DEE2E6")
	cancel_normal.corner_radius_top_left = 6
	cancel_normal.corner_radius_top_right = 6
	cancel_normal.corner_radius_bottom_left = 6
	cancel_normal.corner_radius_bottom_right = 6
	cancel_button.add_theme_stylebox_override("normal", cancel_normal)
	cancel_button.add_theme_color_override("font_color", Color("#495057"))

	# 取消按钮悬停样式
	var cancel_hover = StyleBoxFlat.new()
	cancel_hover.bg_color = Color("#E9ECEF")
	cancel_hover.border_width_left = 2
	cancel_hover.border_width_top = 2
	cancel_hover.border_width_right = 2
	cancel_hover.border_width_bottom = 2
	cancel_hover.border_color = Color("#ADB5BD")
	cancel_hover.corner_radius_top_left = 6
	cancel_hover.corner_radius_top_right = 6
	cancel_hover.corner_radius_bottom_left = 6
	cancel_hover.corner_radius_bottom_right = 6
	cancel_button.add_theme_stylebox_override("hover", cancel_hover)

	# 确认按钮样式
	var confirm_normal = StyleBoxFlat.new()
	confirm_normal.bg_color = Color("#28A745")
	confirm_normal.border_width_left = 2
	confirm_normal.border_width_top = 2
	confirm_normal.border_width_right = 2
	confirm_normal.border_width_bottom = 2
	confirm_normal.border_color = Color("#1E7E34")
	confirm_normal.corner_radius_top_left = 6
	confirm_normal.corner_radius_top_right = 6
	confirm_normal.corner_radius_bottom_left = 6
	confirm_normal.corner_radius_bottom_right = 6
	confirm_button.add_theme_stylebox_override("normal", confirm_normal)
	confirm_button.add_theme_color_override("font_color", Color.WHITE)

	# 确认按钮悬停样式
	var confirm_hover = StyleBoxFlat.new()
	confirm_hover.bg_color = Color("#218838")
	confirm_hover.border_width_left = 2
	confirm_hover.border_width_top = 2
	confirm_hover.border_width_right = 2
	confirm_hover.border_width_bottom = 2
	confirm_hover.border_color = Color("#1E7E34")
	confirm_hover.corner_radius_top_left = 6
	confirm_hover.corner_radius_top_right = 6
	confirm_hover.corner_radius_bottom_left = 6
	confirm_hover.corner_radius_bottom_right = 6
	confirm_button.add_theme_stylebox_override("hover", confirm_hover)

# 静态方法，用于检查内容是否可展开
static func is_content_expandable(content: String) -> bool:
	"""检查内容是否是结构化数据，适合展开"""
	if content.is_empty():
		return false

	# 去除首尾空白字符
	var trimmed_content = content.strip_edges()

	# 检查是否是JSON对象
	if trimmed_content.begins_with("{") and trimmed_content.ends_with("}"):
		return _is_valid_json(trimmed_content)

	# 检查是否是JSON数组
	if trimmed_content.begins_with("[") and trimmed_content.ends_with("]"):
		return _is_valid_json(trimmed_content)

	# 检查是否包含多行内容（可能是格式化的文本）
	if trimmed_content.count("\n") > 0:
		return true

	# 检查是否是长文本（超过100字符可能需要展开查看）
	if trimmed_content.length() > 100:
		return true

	# 检查是否包含特殊字符，可能是编码数据
	if trimmed_content.count("\t") > 0 or trimmed_content.count("\\") > 2:
		return true

	return false

static func _is_valid_json(content: String) -> bool:
	"""检查字符串是否是有效的JSON"""
	var json = JSON.new()
	var parse_result = json.parse(content)
	return parse_result == OK
