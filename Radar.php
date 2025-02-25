<?php

use Firebase\JWT\JWT;
use Firebase\JWT\Key;

include_once("Traffic_Data/config.inc.php");
include_once("Traffic_Data/td.inc.php");
include_once("Traffic_Data/ac.inc.php");

// Set security headers
header("Content-Security-Policy: script-src 'self' http://api.spectrumtraffic.com");
header("Access-Control-Allow-Origin: *");
header("access-control-allow-credentials: true");
header("access-control-allow-headers: content-type");
header("access-control-allow-methods: GET,HEAD,PUT,PATCH,POST,DELETE");
header("access-control-allow-origin: http://localhost:3001");

// Determine the requested action
$act = $_POST['act'] ?? $_GET['act'] ?? null;

function isValidIPv4($ip) {
    return filter_var($ip, FILTER_VALIDATE_IP, FILTER_FLAG_IPV4) !== false;
}

function errorOccured($errorMessage) {
    echo json_encode(["err" => 1, "message" => $errorMessage]);
    exit(0);
}

switch ($act) {

    case "get_schedules":
        $resultant = ["err" => 0];
        $ip_address = $_GET['ip_address'] ?? null;

        if (!isValidIPv4($ip_address)) {
            errorOccured("Invalid IP address format");
        }

        $start = new DateTime("2024-05-07 00:00:00");
        $end = new DateTime("2024-05-07 23:59:59");

        $resultant['schedules'] = [
            ["start" => $start->format('U'), "end" => $end->format('U')]
        ];

        echo json_encode($resultant);
        exit(0);

    case "upload_gzip":
        header("Content-Type: application/json");

        // Check if file was uploaded
        if (!isset($_FILES['file']) || $_FILES['file']['error'] !== 0) {
            errorOccured("No valid file was uploaded.");
        }

        // Validate GZIP file type
        if ($_FILES['file']['type'] !== "application/gzip") {
            errorOccured("Invalid file type: " . $_FILES['file']['type']);
        }

        // Read and decompress GZIP data in memory
        $gzipFilePath = $_FILES['file']['tmp_name'];
        $compressedData = file_get_contents($gzipFilePath);

        if ($compressedData === false) {
            errorOccured("Failed to read uploaded GZIP file.");
        }

        // Decompress GZIP data
        $jsonContent = gzdecode($compressedData);

        if ($jsonContent === false) {
            errorOccured("Failed to decompress GZIP file.");
        }

        // Decode JSON
        $decodedJson = json_decode($jsonContent, true);
        if ($decodedJson === null) {
            errorOccured("Invalid JSON format in GZIP file.");
        }

        // Return JSON response
        echo json_encode([
            "err" => 0,
            "message" => "File uploaded and processed successfully",
            "json_contents" => $decodedJson
        ], JSON_PRETTY_PRINT);
        exit(0);

    default:
        errorOccured("Unknown method");
}
