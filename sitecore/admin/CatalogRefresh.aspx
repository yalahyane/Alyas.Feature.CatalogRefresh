<%@ Page Language="C#" AutoEventWireup="true" %>

<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>Catalog Refresh</title>

    <link rel="stylesheet" href="/sitecore/admin/lib/css/bootstrap.min.css">
    <link rel="stylesheet" href="../../Styles/Libraries/FontAwesome-6.5.1/css/all.min.css">

    <link rel="stylesheet" href="/sitecore/admin/lib/css/bootstrap-multiselect.css">

    <style>
        .header-bar {
            background: #2b2b2b;
            padding: 8px 50px;
        }

            .header-bar img {
                height: 33px;
                width: 33px;
            }

        .breadcrumb-bar {
            background: url('/sitecore/shell/client/Speak/Assets/img/Speak/Layouts/breadcrumb_red_bg.png');
            height: 90px;
            color: #fff;
            display: flex;
            align-items: center;
            padding-left: 30px;
        }

            .breadcrumb-bar h1 {
                margin: 0;
                font-size: 28px;
            }

        .panel-title {
            font-size: 18px;
        }

        #statusMessage {
            display: none;
            margin-top: 15px;
        }
    </style>
</head>
<body>

    <div class="row header-bar">
        <div class="col-md-3">
            <a href="/sitecore/shell/sitecore/client/Applications/Launchpad">
                <img src="/sitecore/shell/client/Speak/Assets/img/Speak/GlobalLogo/global_logo.png"
                    alt="Launchpad" />
            </a>
        </div>
        <div class="col-md-9"></div>
    </div>

    <div class="breadcrumb-bar">
        <h1>Catalog Refresh</h1>
    </div>

    <div class="container" style="margin-top: 30px;">

        <div class="panel panel-default">
            <div class="panel-heading">
                <span class="panel-title"><i class="fa fa-cogs"></i>Refresh Settings</span>
            </div>
            <div class="panel-body">
                <div class="row">

                    <div class="col-md-5">
                        <div class="form-group">
                            <label for="selSource">Source Environment</label>
                            <select id="selSource" class="form-control">
                                <option value="">-- select source --</option>
                                <option value="DEV">DEV</option>
                                <option value="TEST">TEST</option>
                                <option value="STAGE">STAGE</option>
                                <option value="PROD">PROD</option>
                            </select>
                        </div>
                    </div>

                    <div class="col-md-5">
                        <div class="form-group">
                            <label>Destination Environments</label>
                            <select id="selDestination" class="form-control" multiple="multiple">
                                <option value="DEV">DEV</option>
                                <option value="TEST">TEST</option>
                                <option value="STAGE">STAGE</option>
                            </select>
                        </div>
                    </div>

                    <div class="col-md-2" style="padding-top: 25px;">
                        <button id="btnRefresh"
                            class="btn btn-primary btn-block btn-lg"
                            disabled>
                            <i class="fa fa-refresh"></i>Start Refresh
                        </button>
                    </div>

                </div>

                <div id="processingPanel"
                    class="panel panel-info"
                    style="display: none; margin-top: 15px;">
                    <div class="panel-body">
                        <i class="fa fa-clock-o"></i>
                        <span id="stopwatch-time">00:00</span>
                        Processing...
                    </div>
                </div>

                <div id="statusMessage" class="alert" role="alert"></div>
            </div>
        </div>

        <div class="panel panel-default">
            <div class="panel-heading">
                <span class="panel-title"><i class="fa fa-history"></i>Catalog Refresh History</span>
            </div>
            <div class="panel-body" style="max-height: 600px; overflow-y: auto;">
                <table class="table table-striped">
                    <thead>
                        <tr>
                            <th>Status</th>
                            <th>Source</th>
                            <th>Destination</th>
                            <th>Started At</th>
                            <th>Ended At</th>
                            <th>Error Message</th>
                        </tr>
                    </thead>
                    <tbody id="tbJobs"></tbody>
                </table>
            </div>
        </div>

    </div>

    <script src="/sitecore/admin/lib/js/jquery-3.3.1.min.js"></script>
    <script src="/sitecore/admin/lib/js/bootstrap.min.js"></script>
    <script src="/sitecore/admin/lib/js/bootstrap-multiselect.min.js"></script>
    <script src="CatalogRefresh.js"></script>
</body>
</html>
